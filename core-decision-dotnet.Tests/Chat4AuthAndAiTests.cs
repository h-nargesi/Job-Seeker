using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Photon.JobSeeker.Tests;

public class Chat4AuthAndAiTests
{
    private static readonly ApiAuthorization clients = ApiAuthorization.FromConfig(
        "dash-secret", "search-secret", "worker-secret");

    [Theory]
    [InlineData("search-secret", "/decision/take", "POST", true)]
    [InlineData("search-secret", "/decision/scopes", "GET", true)]
    [InlineData("search-secret", "/ai/next", "GET", false)]
    [InlineData("search-secret", "/job/promote", "POST", false)]
    [InlineData("worker-secret", "/ai/next", "GET", true)]
    [InlineData("worker-secret", "/ai/verdict", "POST", true)]
    [InlineData("worker-secret", "/decision/take", "POST", false)]
    [InlineData("dash-secret", "/ai/next", "GET", true)]
    [InlineData("dash-secret", "/job/promote", "POST", true)]
    [InlineData("dash-secret", "/decision/take", "POST", true)]
    public void Api_key_role_controls_path_table(string key, string path, string method, bool allowed)
    {
        Assert.Equal(allowed, clients.TryAuthorize(key, new PathString(path), method, out _));
    }

    [Fact]
    public void Unknown_or_empty_key_is_denied()
    {
        Assert.False(clients.TryAuthorize("nope", new PathString("/ai/next"), "GET", out _));
        Assert.False(clients.TryAuthorize("", new PathString("/ai/next"), "GET", out _));
    }

    [Fact]
    public void Assistant_row_can_be_added_without_rewriting_middleware()
    {
        var extended = new ApiAuthorization(
        [
            .. clients.Clients,
            new ApiClientRole
            {
                Role = "assistant",
                Secret = "asst-secret",
                Rules =
                [
                    new ApiPathRule { Prefix = "/assistant" },
                    new ApiPathRule { Prefix = "/decision/scopes", Method = "GET" },
                ],
            },
        ]);

        Assert.True(extended.TryAuthorize("asst-secret", new PathString("/assistant/jobs"), "GET", out var role));
        Assert.Equal("assistant", role);
        Assert.True(extended.TryAuthorize("asst-secret", new PathString("/decision/scopes"), "GET", out _));
        Assert.False(extended.TryAuthorize("asst-secret", new PathString("/decision/take"), "POST", out _));
        Assert.False(extended.TryAuthorize("asst-secret", new PathString("/ai/next"), "GET", out _));
    }

    [Fact]
    public void Verdict_accepts_locked_payload_and_ignores_delta()
    {
        var json = """
            {"jobId":1,"relevance":80,"verdict":"Match","reason":"fit","seniority":"Senior",
             "salary_min":0,"salary_max":10,"currency":"EUR","period":"Year","work_model":"Remote",
             "contract":"Permanent","experience_years":8,"skills":["C#"],"fingerprint":"abc",
             "delta":{"keys":["DOTNET"]}}
            """;
        var body = JsonSerializer.Deserialize<AiVerdictRequest>(json)!;
        Assert.True(body.TryCreate(out var update, out var error), error);
        Assert.Equal(80, update.AiScore);
        Assert.Equal(AiVerdict.Match, update.AiVerdict);
        Assert.Equal(AiSeniority.Senior, update.AiSeniority);
        Assert.NotNull(body.Delta);
    }

    [Fact]
    public void Verdict_accepts_absent_delta()
    {
        var json = """{"relevance":60,"verdict":"Possible","fingerprint":"abc"}""";
        var body = JsonSerializer.Deserialize<AiVerdictRequest>(json)!;
        Assert.True(body.TryCreate(out _, out var error), error);
        Assert.Null(body.Delta);
    }

    [Theory]
    [InlineData("""{"verdict":"Match","fingerprint":"abc"}""", "relevance")]
    [InlineData("""{"relevance":101,"verdict":"Match","fingerprint":"abc"}""", "relevance")]
    [InlineData("""{"relevance":80,"verdict":"Nope","fingerprint":"abc"}""", "verdict")]
    [InlineData("""{"relevance":80,"verdict":"Match"}""", "fingerprint")]
    [InlineData("""{"relevance":80,"verdict":"Match","fingerprint":"abc","experience_years":51}""", "experience")]
    [InlineData("""{"relevance":80,"verdict":"Match","fingerprint":"abc","salary_min":-1}""", "salary")]
    public void Verdict_rejects_invalid_fields(string json, string hint)
    {
        var body = JsonSerializer.Deserialize<AiVerdictRequest>(json)!;
        Assert.False(body.TryCreate(out _, out var error));
        Assert.Contains(hint, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verdict_rejects_too_many_skills_and_long_reason()
    {
        var body = new AiVerdictRequest
        {
            Relevance = 50,
            Verdict = "NoMatch",
            Fingerprint = "abc",
            Skills = Enumerable.Range(0, 21).Select(i => i.ToString()).ToList(),
        };
        Assert.False(body.TryCreate(out _, out var skills_error));
        Assert.Contains("skills", skills_error);

        body.Skills = ["ok"];
        body.Reason = new string('x', AiVerdictRequest.ReasonMaxLength + 1);
        Assert.False(body.TryCreate(out _, out var reason_error));
        Assert.Contains("reason", reason_error);
    }

    [Fact]
    public async Task Verdict_http_writes_404_when_job_missing_and_400_when_invalid()
    {
        using var db = new GoldenDatabase();
        var controller = new AiController(db.Database, null!, null!, null!);

        var missing = await controller.Verdict(99, ValidBody("deadbeef"));
        Assert.IsType<NotFoundResult>(missing);

        var bad = await controller.Verdict(1, new AiVerdictRequest { Verdict = "Match", Fingerprint = "x" });
        var bad_result = Assert.IsType<BadRequestObjectResult>(bad);
        Assert.Equal(400, bad_result.StatusCode);
    }

    [Fact]
    public async Task Verdict_http_applies_from_ai_pending()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "http-v", JobState.AiPending);
        var controller = new AiController(db.Database, null!, null!, null!);
        var body = ValidBody(JobContent.Fingerprint("job text"));
        body.JobId = id;

        Assert.IsType<OkResult>(await controller.Verdict(null, body));
        Assert.Equal(JobState.Attention, db.Database.Job.Fetch(id)!.State);
    }

    [Fact]
    public void Promote_and_requeue_http_guards()
    {
        using var db = new GoldenDatabase();
        var queued = Seed(db, "p1", JobState.NotApprovedAI);
        var empty = Seed(db, "p2", JobState.AIError, content: null);
        var attention = Seed(db, "p3", JobState.Attention);
        var controller = new JobController(null!, db.Database, null!);

        Assert.IsType<OkResult>(controller.Promote(queued));
        Assert.Equal(JobState.Attention, db.Database.Job.Fetch(queued)!.State);

        Assert.IsType<BadRequestResult>(controller.Promote(attention));
        Assert.IsType<OkResult>(controller.Requeue(Seed(db, "p4", JobState.AIError)));
        Assert.IsType<BadRequestResult>(controller.Requeue(empty));
        Assert.IsType<BadRequestResult>(controller.Requeue(attention));
    }

    [Fact]
    public void Force_revaluate_http_guards_content_and_terminal_states()
    {
        using var db = new GoldenDatabase();
        var controller = new JobController(null!, db.Database, null!);
        Assert.IsType<NotFoundResult>(controller.Revaluate(404));

        var empty = Seed(db, "f1", JobState.AiPending, content: null);
        Assert.IsType<BadRequestResult>(controller.Revaluate(empty));

        var rejected = Seed(db, "f2", JobState.Rejected);
        Assert.IsType<BadRequestResult>(controller.Revaluate(rejected));
    }

    [Fact]
    public void Next_payload_is_data_only()
    {
        var job = new Job { JobID = 7, Content = "hello  world" };
        var keywords = JobKeywords.From(
        [
            EligibilityFixture.Option("field", 90, "c#", "C#"),
            EligibilityFixture.Option("reject", 0, "react", "react"),
        ]);
        var payload = AiNextPayload.From(job, "resume text", keywords, 60);

        Assert.False(payload.Empty);
        Assert.Equal(7, payload.JobId);
        Assert.Equal("hello  world", payload.Content);
        Assert.Equal("resume text", payload.Resume);
        Assert.Equal(JobContent.Fingerprint(job.Content), payload.Fingerprint);
        Assert.Equal(60, payload.Settings!.Aipassmark);
        Assert.Single(payload.Keywords!);
        Assert.Equal("C#", payload.Keywords![0].Title);
        Assert.DoesNotContain("You are", payload.Resume);
    }

    [Fact]
    public void Resume_html_helper_strips_and_caps_from_the_top()
    {
        var text = ResumeHtml.PruneStripCap("<html><head><script>secret()</script></head><body>Hello world</body></html>");
        Assert.Contains("Hello world", text);
        Assert.DoesNotContain("secret", text);

        var long_text = new string('a', ResumeHtml.TokenCap * ResumeHtml.CharsPerToken + 50);
        var capped = ResumeHtml.CapFromTop(long_text);
        Assert.Equal(ResumeHtml.TokenCap * ResumeHtml.CharsPerToken, capped.Length);
        Assert.StartsWith("aaa", capped);
    }

    [Fact]
    public void Master_context_enables_all_skill_keys()
    {
        var context = ResumeHtml.MasterContext();
        Assert.True(context.Keys.DOTNET);
        Assert.True(context.Keys.JAVA);
        Assert.True(context.Keys.NETWORK);
    }

    [Fact]
    public void Force_revaluate_overwrites_human_edited_and_clears_proposals()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("force1", "https://example.com/jobs/force1");
        db.ExecuteRaw(
            "UPDATE Job SET Html = '<html>keep</html>', Content = 'backend alpha beta', State = 'AiPending' WHERE Code = 'force1'");
        var job = db.Database.Job.Fetch(GoldenDatabase.AgencyId, "force1")!;
        job.Options = new ResumeContext { JobTitle = "Keep This Title", HumanEdited = true };
        job.AiOptions = new ResumeContext { JobTitle = "ai" };
        job.ResumeText = new ResumeText
        {
            ["title"] = new ResumeTextSlot { Live = "live-title", Proposal = "proposal-title" },
        };

        var helper = new JobEligibilityHelper(
            EligibilityFixture.CreateDictionaries("alpha", "beta"),
            db.Database,
            [EligibilityFixture.Option("field", 100, "backend", "Backend")]);
        helper.EvaluateJobEligibility(job, null, force: true);

        var stored = db.Database.Job.Fetch(job.JobID)!;
        Assert.NotEqual("Keep This Title", stored.Options?.JobTitle);
        Assert.Null(stored.AiOptions);
        Assert.Equal("live-title", stored.ResumeText!["title"].Live);
        Assert.Null(stored.ResumeText["title"].Proposal);
        Assert.Equal(JobState.AiPending, stored.State);
    }

    private static AiVerdictRequest ValidBody(string fingerprint)
    {
        return new AiVerdictRequest
        {
            Relevance = 80,
            Verdict = nameof(AiVerdict.Match),
            Reason = "fit",
            Fingerprint = fingerprint,
        };
    }

    private static long Seed(GoldenDatabase db, string code, JobState state, string? content = "job text")
    {
        db.SaveSearchJob(code, $"https://example.com/jobs/{code}");
        db.ExecuteRaw(
            @"UPDATE Job SET State = $state, Content = $content WHERE Code = $code",
            ("$state", state.ToString()),
            ("$content", (object?)content ?? DBNull.Value),
            ("$code", code));
        return db.JobId(code);
    }
}
