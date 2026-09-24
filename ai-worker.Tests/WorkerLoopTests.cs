using System.Net;
using System.Text.Json;

namespace AiWorker.Tests;

[Collection("worker-run")]
public sealed class WorkerLoopTests
{
    private const string Rubric = "Judge this posting.";
    private const string RubricTailor = "Tailor this resume.";

    private static LlmOptions Options()
    {
        return new LlmOptions
        {
            BaseUrl = "http://llm.test/v1",
            Model = "test-model",
            Core = "http://core.test",
            CoreApiKey = "worker-key",
            Rubric = Rubric,
            RubricTailor = RubricTailor,
            Temperature = 0.2,
            Seed = 42,
        };
    }

    private static WorkerLoop Loop(FakeHandler coreHandler, FakeHandler llmHandler, LlmOptions? options = null)
    {
        options ??= Options();
        var core = new CoreClient(FakeHttp.Client(coreHandler), options.CoreApiKey);
        var llm = new LlmClient(FakeHttp.Client(llmHandler, "http://llm.test/v1/"), options);
        return new WorkerLoop(core, llm, new PromptBuilder(options.Rubric, options.RubricTailor), options);
    }

    private static string NextJob(long id, string fingerprint = "fp", string contextVersion = "v1")
    {
        return $$"""
        {"empty": false, "jobId": {{id}}, "content": "Senior .NET role with Angular.",
         "fingerprint": "{{fingerprint}}", "contextVersion": "{{contextVersion}}",
         "options": "{\"Length\": 1}"
        }
        """;
    }

    private static string NextEmpty()
    {
        return """{"empty": true}""";
    }

    private static string ContextJson(
        string contextVersion = "v1",
        string rankingField = "", string rankingValue = "",
        string resumeField = "", string resumeValue = "",
        string resume = "MASTER RESUME TEXT")
    {
        var ranking = rankingField.Length == 0
            ? "[]"
            : $$"""[{"domain":"*","fieldKey":"{{rankingField}}","kind":"Tip","value":"{{rankingValue}}"}]""";
        var resume_rows = resumeField.Length == 0
            ? "[]"
            : $$"""[{"domain":"*","fieldKey":"{{resumeField}}","kind":"Correction","value":"{{resumeValue}}"}]""";
        return $$"""
        {"rankingMemory": {{ranking}}, "resumeMemory": {{resume_rows}},
         "keywords": [{"category": "tech", "score": 120, "title": "C#"}],
         "resume": "{{resume}}",
         "inventory": [{"id": "#douran", "type": "block", "keys": ["key-java"], "text": "Java role"}],
         "aiPassmark": 60, "contextVersion": "{{contextVersion}}"}
        """;
    }

    private static void RunReportOk(FakeHandler coreHandler)
    {
        coreHandler.RespondJson("{}");
    }

    private static string LastReportBody(FakeHandler coreHandler)
    {
        var report = coreHandler.Requests.Single(r => r.RequestUri!.AbsolutePath == "/ai/run-report");
        return coreHandler.BodyOf(report);
    }

    private static string LlmContent(string verdictJson)
    {
        var embedded = JsonSerializer.Serialize(verdictJson);
        return "{\"choices\": [{\"message\": {\"role\": \"assistant\", \"content\": " + embedded + "}}]}";
    }

    private static string LlmContentWithMeta(string verdictJson,
        int? promptTokens = null, int? completionTokens = null, string? finishReason = null)
    {
        var embedded = JsonSerializer.Serialize(verdictJson);
        var usage = promptTokens is null || completionTokens is null
            ? "null"
            : $$"""{"prompt_tokens": {{promptTokens}}, "completion_tokens": {{completionTokens}}}""";
        var finish = finishReason is null ? "null" : $"\"{finishReason}\"";
        return "{\"choices\": [{\"message\": {\"role\": \"assistant\", \"content\": " + embedded
            + "}, \"finish_reason\": " + finish + "}], \"usage\": " + usage + "}";
    }

    private const string ValidVerdict =
        """{"relevance": 82, "verdict": "Match", "reason": "ok", "seniority": "Senior", "salary_min": 60000, "salary_max": 80000, "currency": "EUR", "period": "Year", "work_model": "Hybrid", "contract": "Permanent", "experience_years": 5, "skills": [".NET"]}""";

    private const string WeakVerdict =
        """{"relevance": 40, "verdict": "NoMatch", "reason": "weak", "seniority": "Mid", "salary_min": null, "salary_max": null, "currency": null, "period": "Unknown", "work_model": "Onsite", "contract": "Unknown", "experience_years": 2, "skills": []}""";

    private const string ValidDelta =
        """{"keys": ["DOTNET"], "notIncluded": ["#douran"], "included": [], "length": 2, "texts": {"title": "Senior .NET Engineer"}}""";

    private static string LlmSystem(FakeHandler llmHandler, int index)
    {
        return JsonDocument.Parse(llmHandler.Bodies[index]).RootElement
            .GetProperty("messages").EnumerateArray().ToArray()[0].GetProperty("content").GetString()!;
    }

    private static string LlmUser(FakeHandler llmHandler, int index)
    {
        return JsonDocument.Parse(llmHandler.Bodies[index]).RootElement
            .GetProperty("messages").EnumerateArray().ToArray()[1].GetProperty("content").GetString()!;
    }

    [Fact]
    public async Task DrainsQueueAndPostsVerdictWithoutTailoringWhenBelowPassmark()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(11, "fp11"));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent(WeakVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(5, coreHandler.Requests.Count);
        Assert.Single(llmHandler.Requests);

        Assert.Equal("/ai/context", coreHandler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("/ai/next", coreHandler.Requests[1].RequestUri!.AbsolutePath);

        var verdict = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.Equal(11, verdict.GetProperty("jobId").GetInt64());
        Assert.Equal(40, verdict.GetProperty("relevance").GetInt32());
        Assert.Equal("NoMatch", verdict.GetProperty("verdict").GetString());
        Assert.Equal("fp11", verdict.GetProperty("fingerprint").GetString());
        Assert.False(verdict.TryGetProperty("delta", out _));

        foreach (var request in coreHandler.Requests)
        {
            Assert.Equal("worker-key", request.Headers.GetValues("X-API-Key").Single());
            Assert.Equal("worker", request.Headers.GetValues("X-Client").Single());
        }

        var chat = JsonDocument.Parse(llmHandler.Bodies[0]).RootElement;
        Assert.Equal("test-model", chat.GetProperty("model").GetString());
        Assert.Equal(0.2, chat.GetProperty("temperature").GetDouble());
        Assert.Equal(42, chat.GetProperty("seed").GetInt32());
        Assert.Equal(LlmOptions.DefaultMaxCompletionTokens, chat.GetProperty("max_tokens").GetInt32());
        Assert.True(chat.GetProperty("cache_prompt").GetBoolean());
        Assert.Equal(LlmOptions.DefaultSlot, chat.GetProperty("id_slot").GetInt32());
        Assert.Equal("json_schema", chat.GetProperty("response_format").GetProperty("type").GetString());
        Assert.False(chat.GetProperty("stream").GetBoolean());

        var messages = chat.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        var system = messages[0].GetProperty("content").GetString()!;
        Assert.Contains(PromptBuilder.TrunkPreamble.Split('\n')[0], system);
        Assert.Contains(PromptBuilder.KeywordsLabel, system);
        Assert.Contains("\"title\":\"C#\"", system);
        Assert.Contains(PromptBuilder.RankingMemoryLabel, system);
        Assert.Contains(PromptBuilder.ResumeLabel, system);
        Assert.Contains("MASTER RESUME TEXT", system);
        Assert.Contains(PromptBuilder.InventoryLabel, system);
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        var user = messages[1].GetProperty("content").GetString()!;
        Assert.StartsWith(PromptBuilder.JobLabel, user);
        Assert.Contains("Senior .NET role with Angular.", user);
        Assert.Contains(PromptBuilder.TaskRankingLabel, user);
        Assert.Contains(PromptBuilder.ApplyThreshold(60), user);
        Assert.Contains(Rubric, user);
        Assert.DoesNotContain(PromptBuilder.ResumeLabel, user);
    }

    [Fact]
    public async Task PromotingVerdictRunsCall2WithSharedTrunkAndIdenticalJobPosting()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(21));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent(ValidVerdict));
        llmHandler.RespondJson(LlmContent(ValidDelta));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(2, llmHandler.Requests.Count);
        Assert.Single(coreHandler.Requests, r => r.RequestUri!.AbsolutePath == "/ai/context");

        var verdict = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.True(verdict.TryGetProperty("delta", out var delta));
        Assert.Equal("Senior .NET Engineer", delta.GetProperty("texts").GetProperty("title").GetString());

        var tailor_chat = JsonDocument.Parse(llmHandler.Bodies[1]).RootElement;
        Assert.Equal("resume_tailoring_delta", tailor_chat.GetProperty("response_format")
            .GetProperty("json_schema").GetProperty("name").GetString());

        Assert.Equal(LlmSystem(llmHandler, 0), LlmSystem(llmHandler, 1));

        var tailor_user = LlmUser(llmHandler, 1);
        Assert.StartsWith(PromptBuilder.JobLabel, tailor_user);
        Assert.Contains("Senior .NET role with Angular.", tailor_user);
        Assert.Contains(PromptBuilder.SelectionLabel, tailor_user);
        Assert.Contains(PromptBuilder.EmptySelection, tailor_user);
        Assert.Contains(PromptBuilder.TaskTailoringLabel, tailor_user);
        Assert.Contains(RubricTailor, tailor_user);
        Assert.DoesNotContain("Apply threshold:", tailor_user);
    }

    [Fact]
    public async Task Call2ModelOutputFailureRetriesThenPostsVerdictAlone()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(22));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent(ValidVerdict));
        llmHandler.RespondJson(LlmContent("not json"));
        llmHandler.RespondJson(LlmContent("""{"keys": 5}"""));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(3, llmHandler.Requests.Count);
        var verdict = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.Equal("Match", verdict.GetProperty("verdict").GetString());
        Assert.False(verdict.TryGetProperty("delta", out _));
    }

    [Fact]
    public async Task Call2ConnectionFailureAbortsRunAndWritesNothing()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(23));
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent(ValidVerdict));
        llmHandler.RespondNetworkError();

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitLlmUnavailable, exit);
        Assert.Equal(3, coreHandler.Requests.Count);
        Assert.DoesNotContain(coreHandler.Requests, r => r.RequestUri!.AbsolutePath == "/ai/verdict");
        Assert.Equal(WorkerLoop.ExitLlmUnavailable,
            JsonDocument.Parse(LastReportBody(coreHandler)).RootElement.GetProperty("exitCode").GetInt32());
    }

    [Fact]
    public async Task ErrorVerdictSkipsCall2()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(24));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent("oops not json"));
        llmHandler.RespondJson(LlmContent("""{"verdict": "Match"}"""));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(2, llmHandler.Requests.Count);
        var verdict = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.Equal("Error", verdict.GetProperty("verdict").GetString());
        Assert.False(verdict.TryGetProperty("delta", out _));
    }

    [Fact]
    public async Task TwoBadModelOutputsPostErrorVerdict()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(12));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent("oops not json"));
        llmHandler.RespondJson(LlmContent("""{"verdict": "Match"}"""));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(2, llmHandler.Requests.Count);
        var verdict = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.Equal("Error", verdict.GetProperty("verdict").GetString());
        Assert.Equal(0, verdict.GetProperty("relevance").GetInt32());
        Assert.Contains("worker:", verdict.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ModelNetworkFailureAbortsRunAndWritesNothing()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(13));
        RunReportOk(coreHandler);
        llmHandler.RespondNetworkError();

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitLlmUnavailable, exit);
        Assert.Equal(3, coreHandler.Requests.Count);
        Assert.DoesNotContain(coreHandler.Requests, r => r.RequestUri!.AbsolutePath == "/ai/verdict");
    }

    [Fact]
    public async Task ModelHttp500PostsErrorVerdictAndContinues()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(14));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextJob(15));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson("boom", HttpStatusCode.InternalServerError);
        llmHandler.RespondJson(LlmContent(WeakVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(2, llmHandler.Requests.Count);
        var first = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.Equal("Error", first.GetProperty("verdict").GetString());
        Assert.Contains("model endpoint returned 500", first.GetProperty("reason").GetString());
        var second = JsonDocument.Parse(coreHandler.Bodies[1]).RootElement;
        Assert.Equal("NoMatch", second.GetProperty("verdict").GetString());
    }

    [Fact]
    public async Task ModelTimeoutPostsErrorVerdictAndContinues()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(18));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextJob(19));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondTimeout();
        llmHandler.RespondJson(LlmContent(WeakVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(2, llmHandler.Requests.Count);
        var first = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.Equal("Error", first.GetProperty("verdict").GetString());
        Assert.Contains("timed out after 120s", first.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task PersistentModel5xxAbortsRunAfterConsecutiveJobs()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(20));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextJob(21));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextJob(22));
        RunReportOk(coreHandler);
        for (var i = 0; i < 3; i++) llmHandler.RespondJson("boom", HttpStatusCode.InternalServerError);

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitLlmUnavailable, exit);
        Assert.Equal(3, llmHandler.Requests.Count);
        var verdicts = coreHandler.Requests
            .Where(r => r.RequestUri!.AbsolutePath == "/ai/verdict")
            .Select(coreHandler.BodyOf)
            .ToList();
        Assert.Equal(2, verdicts.Count);
        Assert.All(verdicts, body =>
            Assert.Equal("Error", JsonDocument.Parse(body).RootElement.GetProperty("verdict").GetString()));
    }

    [Fact]
    public async Task TailorCallFailurePostsVerdictWithoutDelta()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(25));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent(ValidVerdict));
        llmHandler.RespondJson("boom", HttpStatusCode.InternalServerError);

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(2, llmHandler.Requests.Count);
        var verdict = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.Equal("Match", verdict.GetProperty("verdict").GetString());
        Assert.False(verdict.TryGetProperty("delta", out _));
    }

    [Fact]
    public async Task ModelOutputRetryUsesDifferentSeed()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(26));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent("oops not json"));
        llmHandler.RespondJson(LlmContent(WeakVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        var first_seed = JsonDocument.Parse(llmHandler.Bodies[0]).RootElement.GetProperty("seed").GetInt32();
        var second_seed = JsonDocument.Parse(llmHandler.Bodies[1]).RootElement.GetProperty("seed").GetInt32();
        Assert.Equal(42, first_seed);
        Assert.Equal(43, second_seed);
    }

    [Fact]
    public async Task Verdict404ContinuesWithNextJob()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(15));
        coreHandler.RespondJson("""{"error": "not found"}""", HttpStatusCode.NotFound);
        coreHandler.RespondJson(NextJob(16));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent(ValidVerdict));
        llmHandler.RespondJson(LlmContent(ValidDelta));
        llmHandler.RespondJson(LlmContent(WeakVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(7, coreHandler.Requests.Count);
        Assert.Equal(3, llmHandler.Requests.Count);
        var lastVerdict = JsonDocument.Parse(coreHandler.Bodies[1]).RootElement;
        Assert.Equal(16, lastVerdict.GetProperty("jobId").GetInt64());
    }

    [Fact]
    public async Task Verdict400AbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(17));
        coreHandler.RespondJson("""{"error": "validation", "message": "bad"}""", HttpStatusCode.BadRequest);
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent(WeakVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitCoreAbort, exit);
    }

    [Fact]
    public async Task NextNon200AbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson("oops", HttpStatusCode.InternalServerError);
        RunReportOk(coreHandler);

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitCoreAbort, exit);
        Assert.Empty(llmHandler.Requests);
    }

    [Fact]
    public async Task NextNetworkErrorAbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondNetworkError("core down");
        RunReportOk(coreHandler);

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitCoreAbort, exit);
        Assert.Empty(llmHandler.Requests);
    }

    [Fact]
    public async Task ContextFetchFailureAbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson("oops", HttpStatusCode.InternalServerError);
        RunReportOk(coreHandler);

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitCoreAbort, exit);
        Assert.Equal(2, coreHandler.Requests.Count);
        Assert.Equal("/ai/context", coreHandler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("/ai/run-report", coreHandler.Requests[1].RequestUri!.AbsolutePath);
        Assert.Empty(llmHandler.Requests);
    }

    [Fact]
    public async Task ContextWithoutResumeAbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson(resume: ""));
        RunReportOk(coreHandler);

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitCoreAbort, exit);
        Assert.Empty(llmHandler.Requests);
    }

    [Fact]
    public async Task UsageAndFinishReasonAreCarriedThrough()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(41));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContentWithMeta(ValidVerdict, 1500, 220, "stop"));
        llmHandler.RespondJson(LlmContentWithMeta(ValidDelta, 3000, 350, "stop"));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        var verdict = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.Equal("Match", verdict.GetProperty("verdict").GetString());

        var report = JsonDocument.Parse(LastReportBody(coreHandler)).RootElement;
        Assert.Equal(4500, report.GetProperty("promptTokens").GetInt64());
        Assert.Equal(570, report.GetProperty("completionTokens").GetInt64());
        Assert.Equal(WorkerLoop.ExitOk, report.GetProperty("exitCode").GetInt32());
        Assert.Equal("test-model", report.GetProperty("model").GetString());
        Assert.Equal(0.2, report.GetProperty("temperature").GetDouble());
        Assert.Equal(42, report.GetProperty("seed").GetInt32());
        Assert.Equal(RunReport.ShortHash(Rubric), report.GetProperty("rubricHash").GetString());
        Assert.Equal(RunReport.ShortHash(RubricTailor), report.GetProperty("rubricTailorHash").GetString());
        Assert.True(report.GetProperty("wallSeconds").GetDouble() >= 0);
    }

    [Fact]
    public async Task FinishReasonLengthWithInvalidOutputStillYieldsErrorVerdict()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(42));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContentWithMeta("oops not json", 100, 4000, "length"));
        llmHandler.RespondJson(LlmContentWithMeta("""{"verdict": "Match"}""", 100, 4000, "length"));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(2, llmHandler.Requests.Count);
        var verdict = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.Equal("Error", verdict.GetProperty("verdict").GetString());
        Assert.False(verdict.TryGetProperty("delta", out _));

        var report = JsonDocument.Parse(LastReportBody(coreHandler)).RootElement;
        Assert.Equal(2, report.GetProperty("finishReasonLength").GetInt32());
        Assert.Equal(1, report.GetProperty("errorVerdicts").GetInt32());
        Assert.Equal(2, report.GetProperty("retries").GetInt32());
        var error_ids = report.GetProperty("errorJobIds").EnumerateArray().ToArray();
        Assert.Single(error_ids);
        Assert.Equal(42, error_ids[0].GetInt64());
    }

    [Fact]
    public async Task MemoryRowsAreFetchedOnceAndInjectedIntoTheTrunk()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson(
            rankingField: "remote_only", rankingValue: "remote roles only",
            resumeField: "summary", resumeValue: "lead with backend scale"));
        coreHandler.RespondJson(NextJob(31));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent(ValidVerdict));
        llmHandler.RespondJson(LlmContent(ValidDelta));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(5, coreHandler.Requests.Count);
        Assert.Single(coreHandler.Requests, request => request.RequestUri!.AbsolutePath == "/ai/context");

        var judge_system = LlmSystem(llmHandler, 0);
        Assert.Contains(PromptBuilder.RankingMemoryLabel, judge_system);
        Assert.Contains("remote_only", judge_system);
        Assert.Contains("remote roles only", judge_system);
        Assert.Contains(PromptBuilder.ResumeMemoryLabel, judge_system);
        Assert.Contains("lead with backend scale", judge_system);

        Assert.Equal(judge_system, LlmSystem(llmHandler, 1));
    }

    [Fact]
    public async Task ContextVersionMismatchTriggersExactlyOneRefetchAtTheJobBoundary()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson(rankingField: "remote_only", rankingValue: "remote roles only"));
        coreHandler.RespondJson(NextJob(61));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextJob(62, contextVersion: "v2"));
        coreHandler.RespondJson(ContextJson(contextVersion: "v2", rankingValue: "changed lesson",
            rankingField: "remote_only"));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent(WeakVerdict));
        llmHandler.RespondJson(LlmContent(WeakVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(2, llmHandler.Requests.Count);
        var paths = coreHandler.Requests.Select(r => r.RequestUri!.AbsolutePath).ToList();
        Assert.Equal(
        [
            "/ai/context", "/ai/next", "/ai/verdict",
            "/ai/next", "/ai/context", "/ai/verdict",
            "/ai/next", "/ai/run-report",
        ], paths);

        var first_system = LlmSystem(llmHandler, 0);
        var second_system = LlmSystem(llmHandler, 1);
        Assert.Contains("remote roles only", first_system);
        Assert.Contains("changed lesson", second_system);
        Assert.NotEqual(first_system, second_system);
    }

    [Fact]
    public async Task MatchingContextVersionNeverRefetches()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(63));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextJob(64));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent(WeakVerdict));
        llmHandler.RespondJson(LlmContent(WeakVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Single(coreHandler.Requests, r => r.RequestUri!.AbsolutePath == "/ai/context");
        Assert.Equal(LlmSystem(llmHandler, 0), LlmSystem(llmHandler, 1));
    }

    [Fact]
    public async Task InterruptedRunPostsReportBeforeRethrowing()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(ContextJson());
        coreHandler.RespondJson(NextJob(51));
        RunReportOk(coreHandler);

        var cancellation = new CancellationTokenSource();
        llmHandler.Respond(_ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Loop(coreHandler, llmHandler).RunAsync(cancellation.Token));

        var report = JsonDocument.Parse(LastReportBody(coreHandler)).RootElement;
        Assert.Equal(WorkerLoop.ExitInterrupted, report.GetProperty("exitCode").GetInt32());
        Assert.Equal(1, report.GetProperty("jobs").GetInt32());
    }

    [Fact]
    public async Task TruncationAndDroppedMemoryCountersReachTheReport()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        var long_value = new string('m', 3000);
        var row = $$"""{"domain":"*","fieldKey":"remote_only","kind":"Tip","value":"{{long_value}}"}""";
        var ranking = "[" + string.Join(",", Enumerable.Repeat(row, 3)) + "]";
        coreHandler.RespondJson("{\"rankingMemory\": " + ranking + ", \"resumeMemory\": []," +
            " \"keywords\": [], \"resume\": \"RESUME TEXT\"," +
            " \"inventory\": [{\"id\": \"t\", \"type\": \"slot\", \"text\": \"x\"}]," +
            " \"aiPassmark\": 90, \"contextVersion\": \"v1\"}");
        var huge = new string('j', 60000);
        coreHandler.RespondJson($$$"""
        {"empty": false, "jobId": 52, "content": "{{{huge}}}",
         "fingerprint": "fp52", "contextVersion": "v1"}
        """);
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        RunReportOk(coreHandler);
        llmHandler.RespondJson(LlmContent(ValidVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        var report = JsonDocument.Parse(LastReportBody(coreHandler)).RootElement;
        Assert.Equal(1, report.GetProperty("truncatedJobs").GetInt32());
        Assert.True(report.GetProperty("droppedMemoryRows").GetInt32() >= 1);
    }
}
