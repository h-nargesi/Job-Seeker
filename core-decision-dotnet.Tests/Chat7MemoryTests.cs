using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Photon.JobSeeker.Tests;

public class Chat7MemoryTests
{
    private static readonly ApiAuthorization clients = ApiAuthorization.FromConfig(
        "dash-secret", "search-secret", "worker-secret", "asst-secret");

    [Theory]
    [InlineData("asst-secret", "/assistant/jobs", "GET", true)]
    [InlineData("asst-secret", "/assistant/memory", "GET", true)]
    [InlineData("asst-secret", "/assistant/memorysave", "POST", true)]
    [InlineData("asst-secret", "/assistant/memoryedit", "POST", true)]
    [InlineData("asst-secret", "/assistant/memoryconfirm", "POST", true)]
    [InlineData("asst-secret", "/assistant/memorydelete", "POST", true)]
    [InlineData("asst-secret", "/assistant/applied", "POST", true)]
    [InlineData("asst-secret", "/decision/scopes", "GET", true)]
    [InlineData("asst-secret", "/decision/take", "POST", false)]
    [InlineData("asst-secret", "/decision/orders", "GET", false)]
    [InlineData("asst-secret", "/ai/next", "GET", false)]
    [InlineData("asst-secret", "/ai/context", "GET", false)]
    [InlineData("search-secret", "/assistant/jobs", "GET", false)]
    [InlineData("worker-secret", "/assistant/memory", "GET", false)]
    [InlineData("worker-secret", "/ai/context", "GET", true)]
    [InlineData("worker-secret", "/assistant/applied", "POST", false)]
    [InlineData("dash-secret", "/assistant/jobs", "GET", true)]
    public void Api_key_role_controls_assistant_path_table(string key, string path, string method, bool allowed)
    {
        Assert.Equal(allowed, clients.TryAuthorize(key, new PathString(path), method, out var role));
        if (allowed) Assert.NotEmpty(role);
    }

    [Fact]
    public void Assistant_key_registration_is_a_row_not_a_rewrite()
    {
        var without = ApiAuthorization.FromConfig("dash-secret", "search-secret", "worker-secret");
        var unconfigured = Assert.Single(without.Clients, c => c.Role == ApiAuthorization.AssistantRole);
        Assert.Null(unconfigured.Secret);
        Assert.False(without.TryAuthorize("asst-secret", new PathString("/assistant/jobs"), "GET", out _));

        var with = ApiAuthorization.FromConfig("dash-secret", "search-secret", "worker-secret", "asst-secret");
        var assistant = Assert.Single(with.Clients, c => c.Role == ApiAuthorization.AssistantRole);
        Assert.Equal("asst-secret", assistant.Secret);
        Assert.Equal(2, assistant.Rules.Count);
    }

    [Fact]
    public void Snapshot_returns_confirmed_only_in_precedence_order()
    {
        using var db = new GoldenDatabase();
        var unconfirmed_correction = Insert(db, MemoryScope.Ranking, "remote_only", MemoryKind.Correction, confirmed: false);
        var confirmed_tip = Insert(db, MemoryScope.Ranking, "visa_sponsorship", MemoryKind.Tip, confirmed: true);
        var confirmed_correction = Insert(db, MemoryScope.Ranking, "relocation", MemoryKind.Correction, confirmed: true);
        Insert(db, MemoryScope.Resume, "summary", MemoryKind.Tip, confirmed: true);

        var snapshot = db.Database.Memory.Snapshot(MemoryScope.Ranking, 10);

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(confirmed_correction, snapshot[0].MemoryID);
        Assert.Equal(confirmed_tip, snapshot[1].MemoryID);
        Assert.DoesNotContain(snapshot, row => row.MemoryID == unconfirmed_correction);
    }

    [Fact]
    public void Snapshot_ordering_is_deterministic_and_bump_immune()
    {
        using var db = new GoldenDatabase();
        var domain_tip = Insert(db, MemoryScope.Ranking, "remote_only", MemoryKind.Tip, confirmed: true, domain: "linkedin.com");
        var global_a = Insert(db, MemoryScope.Ranking, "remote_only", MemoryKind.Tip, confirmed: true, domain: "*");
        var relocation = Insert(db, MemoryScope.Ranking, "relocation", MemoryKind.Tip, confirmed: true, domain: "*");

        var snapshot = db.Database.Memory.Snapshot(MemoryScope.Ranking, 10);

        Assert.Equal(relocation, snapshot[0].MemoryID);
        Assert.Equal(Math.Min(domain_tip, global_a), snapshot[1].MemoryID);
        Assert.Equal(Math.Max(domain_tip, global_a), snapshot[2].MemoryID);

        db.Database.Memory.BumpUseCount(relocation);
        db.Database.Memory.BumpUseCount(global_a);

        var bumped = db.Database.Memory.Snapshot(MemoryScope.Ranking, 10);
        Assert.Equal(snapshot.Select(row => row.MemoryID), bumped.Select(row => row.MemoryID));
    }

    [Fact]
    public void Snapshot_is_capped_by_memorycap_setting()
    {
        using var db = new GoldenDatabase();
        db.ExecuteRaw("INSERT INTO AppSetting (Key, Value) VALUES ('memorycap', '2')");
        Insert(db, MemoryScope.Ranking, "remote_only", MemoryKind.Tip, confirmed: true);
        Insert(db, MemoryScope.Ranking, "relocation", MemoryKind.Tip, confirmed: true);
        Insert(db, MemoryScope.Ranking, "no_staffing", MemoryKind.Tip, confirmed: true);
        Insert(db, MemoryScope.Ranking, "salary_floor", MemoryKind.Tip, confirmed: false);
        Insert(db, MemoryScope.Resume, "summary", MemoryKind.Tip, confirmed: true);

        var ranking = db.Database.Memory.Snapshot(MemoryScope.Ranking, db.Database.AppSetting.MemoryCap());
        var resume = db.Database.Memory.Snapshot(MemoryScope.Resume, db.Database.AppSetting.MemoryCap());

        Assert.Equal(2, ranking.Count);
        Assert.Single(resume);
        Assert.Equal("no_staffing", ranking[0].FieldKey);
        Assert.Equal("relocation", ranking[1].FieldKey);
        Assert.Equal(MemoryKind.Tip, ranking[0].Kind);
    }

    [Fact]
    public void Context_payload_version_tracks_run_constants_only()
    {
        using var db = new GoldenDatabase();
        Insert(db, MemoryScope.Ranking, "remote_only", MemoryKind.Tip, confirmed: true);
        var ranking = db.Database.Memory.Snapshot(MemoryScope.Ranking, db.Database.AppSetting.MemoryCap());
        var resume = db.Database.Memory.Snapshot(MemoryScope.Resume, db.Database.AppSetting.MemoryCap());
        var keywords = JobKeywords.From([EligibilityFixture.Option("field", 90, "c#", "C#")]);

        var payload = AiContextPayload.From(ranking, resume, keywords, "resume text", null, 60);
        var again = AiContextPayload.From(ranking, resume, keywords, "resume text", null, 60);

        Assert.NotEmpty(payload.ContextVersion);
        Assert.Equal(payload.ContextVersion, again.ContextVersion);
        Assert.Equal(payload.ContextVersion, AiContextPayload.VersionOf(payload));
        Assert.Single(payload.RankingMemory);
        Assert.Equal(60, payload.AiPassmark);

        db.Database.Memory.BumpUseCount(ranking[0].MemoryID);
        var bumped = db.Database.Memory.Snapshot(MemoryScope.Ranking, db.Database.AppSetting.MemoryCap());
        var after_bump = AiContextPayload.From(bumped, resume, keywords, "resume text", null, 60);
        Assert.Equal(payload.ContextVersion, after_bump.ContextVersion);

        var changed = AiContextPayload.From(bumped, resume, keywords, "new resume text", null, 60);
        Assert.NotEqual(payload.ContextVersion, changed.ContextVersion);
    }

    [Fact]
    public void Context_payload_serializes_to_the_worker_contract()
    {
        var payload = AiContextPayload.From([], [], [], "resume text", new ResumeInventory([]), 60);

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        foreach (var field in "rankingMemory resumeMemory keywords resume inventory aiPassmark contextVersion".Split(' '))
            Assert.Contains($"\"{field}\":", json);
    }

    [Fact]
    public void Ranking_memory_rejects_unknown_field_keys()
    {
        using var db = new GoldenDatabase();
        var controller = new AssistantController(db.Database);

        var bad = controller.MemorySave(new AssistantMemoryRequest
        {
            Scope = nameof(MemoryScope.Ranking),
            Kind = nameof(MemoryKind.Tip),
            FieldKey = "definitely_not_a_ranking_key",
            Value = "no staffing agencies",
        });
        Assert.IsType<BadRequestObjectResult>(bad);

        var good = controller.MemorySave(new AssistantMemoryRequest
        {
            Scope = nameof(MemoryScope.Ranking),
            Kind = nameof(MemoryKind.Tip),
            FieldKey = MemoryRankingKeys.NoStaffing,
            Value = "no staffing agencies",
            Confirmed = true,
        });
        Assert.IsType<OkObjectResult>(good);

        var rows = db.Database.Memory.List(MemoryScope.Ranking);
        var row = Assert.Single(rows);
        Assert.Equal("no_staffing", row.FieldKey);
        Assert.True(row.Confirmed);
        Assert.Equal(MemoryKind.Tip, row.Kind);
    }

    [Fact]
    public void Memory_crud_edit_confirm_delete_round_trip()
    {
        using var db = new GoldenDatabase();
        var controller = new AssistantController(db.Database);
        var id = db.Database.Memory.Insert(new MemoryRow
        {
            Scope = MemoryScope.Apply,
            FieldKey = "first_name",
            Kind = MemoryKind.Correction,
            Value = "Hamed",
            Confirmed = false,
        });

        Assert.IsType<OkResult>(controller.MemoryConfirm(id, true));
        Assert.True(db.Database.Memory.Fetch(id)!.Confirmed);

        Assert.IsType<OkResult>(controller.MemoryEdit(id, new AssistantMemoryRequest { Value = "Ryan" }));
        Assert.Equal("Ryan", db.Database.Memory.Fetch(id)!.Value);

        Assert.IsType<NotFoundResult>(controller.MemoryEdit(404, new AssistantMemoryRequest { Value = "x" }));
        Assert.IsType<NotFoundResult>(controller.MemoryConfirm(404, true));

        Assert.IsType<OkResult>(controller.MemoryDelete(id));
        Assert.Empty(db.Database.Memory.List());
        Assert.IsType<NotFoundResult>(controller.MemoryDelete(id));
    }

    [Fact]
    public void Memory_query_matches_confirmed_exact_domain_before_wildcard()
    {
        using var db = new GoldenDatabase();
        var wildcard = Insert(db, MemoryScope.Apply, "phone", MemoryKind.Tip, confirmed: true, domain: "*");
        var exact = Insert(db, MemoryScope.Apply, "phone", MemoryKind.Tip, confirmed: true, domain: "greenhouse.io");
        Insert(db, MemoryScope.Apply, "phone", MemoryKind.Tip, confirmed: false, domain: "workday.com");

        var matches = db.Database.Memory.Query(MemoryScope.Apply, "greenhouse.io", "phone");

        Assert.Equal(2, matches.Count);
        Assert.Equal(exact, matches[0].MemoryID);
        Assert.Equal(wildcard, matches[1].MemoryID);
    }

    [Fact]
    public void Applied_is_idempotent_and_logs_source_once()
    {
        using var db = new GoldenDatabase();
        var jobid = SeedJob(db, JobState.Attention);
        var controller = new AssistantController(db.Database);

        Assert.IsType<OkResult>(controller.Applied(jobid));
        Assert.IsType<OkResult>(controller.Applied(jobid));
        Assert.IsType<NotFoundResult>(controller.Applied(404));

        var job = db.Database.Job.Fetch(jobid)!;
        Assert.Equal(JobState.Applied, job.State);
        Assert.Equal(1, job.Log!.Split(JobBusiness.AppliedViaAssistant, StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task Jobs_lists_attention_only_with_pending_flag_and_live_text()
    {
        using var db = new GoldenDatabase();
        var attention = SeedJob(db, JobState.Attention);
        db.ExecuteRaw(
            @"UPDATE Job SET ResumeText = $text WHERE JobID = $id",
            ("$text", "{\"title\":{\"live\":\"Live Title\",\"proposal\":\"AI Proposal\",\"status\":\"pending\"}}"),
            ("$id", attention));
        SeedJob(db, JobState.AiPending);

        var services = new ServiceCollection()
            .AddSingleton<IViewRenderService>(new FakeViews())
            .BuildServiceProvider();

        var controller = new AssistantController(db.Database)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services },
            },
        };

        var result = await controller.Jobs();

        var items = Assert.IsType<OkObjectResult>(result).Value as List<AssistantJobItem>;
        var item = Assert.Single(items!);
        Assert.Equal(attention, item.JobId);
        Assert.True(item.PendingProposal);
        Assert.Contains("Live Title", item.ResumeText);
        Assert.DoesNotContain("AI Proposal", item.ResumeText);
        Assert.Contains("Base summary.", item.ResumeText);
    }

    [Fact]
    public void Assistant_memory_request_validates_locked_names_and_lengths()
    {
        var body = JsonSerializer.Deserialize<AssistantMemoryRequest>(
            """{"scope":"ranking","kind":"tip","fieldKey":"remote_only","value":"remote only","domain":"LinkedIn.COM","confirmed":true}""")!;

        Assert.True(body.TryCreate(out var row, out var error), error);
        Assert.Equal(MemoryScope.Ranking, row.Scope);
        Assert.Equal(MemoryKind.Tip, row.Kind);
        Assert.Equal("linkedin.com", row.AgencyDomain);
        Assert.True(row.Confirmed);

        var long_value = new AssistantMemoryRequest
        {
            Scope = nameof(MemoryScope.Apply),
            Kind = nameof(MemoryKind.Tip),
            FieldKey = "phone",
            Value = new string('x', AssistantMemoryRequest.ValueMaxLength + 1),
        };
        Assert.False(long_value.TryCreate(out _, out var value_error));
        Assert.Contains("value", value_error);

        var bad_scope = JsonSerializer.Deserialize<AssistantMemoryRequest>(
            """{"scope":"magic","kind":"tip","fieldKey":"phone","value":"x"}""")!;
        Assert.False(bad_scope.TryCreate(out _, out var scope_error));
        Assert.Contains("scope", scope_error);
    }

    [Fact]
    public void Live_text_overlay_maps_slot_selectors_onto_the_dom()
    {
        var html = """
            <html><body>
            <h1 class="header-job-title">Template Title</h1>
            <p id="summary">Template summary.</p>
            <article id="douran"><ul><li>first</li><li>second</li></ul></article>
            </body></html>
            """;
        var live = new Dictionary<string, string>
        {
            ["title"] = "Live Title",
            ["summary"] = "Live summary.",
            ["#douran li:nth-child(2)"] = "Rewritten bullet.",
        };

        var text = ResumeHtml.RenderText(html, live);

        Assert.Contains("Live Title", text);
        Assert.Contains("Live summary.", text);
        Assert.Contains("Rewritten bullet.", text);
        Assert.DoesNotContain("Template Title", text);
        Assert.DoesNotContain("second", text);
    }

    private sealed class FakeViews : IViewRenderService
    {
        public Task<string> RenderToStringAsync(HttpContext httpContext, string viewName, object model)
        {
            return Task.FromResult("<html><body><h1 class='header-job-title'>Template Title</h1>" +
                "<p id='summary'>Base summary.</p></body></html>");
        }
    }

    private static long Insert(GoldenDatabase db, MemoryScope scope, string fieldKey, MemoryKind kind,
        bool confirmed, string domain = "*")
    {
        return db.Database.Memory.Insert(new MemoryRow
        {
            Scope = scope,
            AgencyDomain = domain,
            FieldKey = fieldKey,
            Kind = kind,
            Confirmed = confirmed,
            Value = "lesson",
        });
    }

    private static long SeedJob(GoldenDatabase db, JobState state)
    {
        var code = $"chat7-{state}-{Guid.NewGuid():N}"[..40];
        db.SaveSearchJob(code, $"https://example.com/jobs/{code}");
        db.ExecuteRaw(
            @"UPDATE Job SET State = $state WHERE Code = $code",
            ("$state", state.ToString()),
            ("$code", code));
        return db.JobId(code);
    }
}
