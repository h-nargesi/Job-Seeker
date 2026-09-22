using System.Net;
using System.Text.Json;

namespace AiWorker.Tests;

public sealed class WorkerLoopTests
{
    private const string Rubric = "Judge this posting. Keywords: {{keywords}}.";
    private const string RubricTailor = "Tailor this resume. Keywords: {{keywords}}.";

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
        return new WorkerLoop(core, llm, new PromptBuilder(options.Rubric, options.RubricTailor));
    }

    private static string NextJob(long id, string fingerprint = "fp", int passmark = 60)
    {
        return $$"""
        {"empty": false, "jobId": {{id}}, "content": "Senior .NET role with Angular.",
         "resume": "RESUME TEXT", "keywords": [{"category": "tech", "score": 120, "title": "C#"}],
         "fingerprint": "{{fingerprint}}", "settings": {"aipassmark": {{passmark}}},
         "options": "{\"Length\": 1}", "inventory": [{"id": "#douran", "type": "block", "keys": ["key-java"], "text": "Java role"}]
        }
        """;
    }

    private static string NextEmpty()
    {
        return """{"empty": true}""";
    }

    private static string MemorySnapshotJson(
        string rankingField = "", string rankingValue = "",
        string resumeField = "", string resumeValue = "")
    {
        var ranking = rankingField.Length == 0
            ? "[]"
            : $$"""[{"domain":"*","fieldKey":"{{rankingField}}","kind":"Tip","value":"{{rankingValue}}"}]""";
        var resume = resumeField.Length == 0
            ? "[]"
            : $$"""[{"domain":"*","fieldKey":"{{resumeField}}","kind":"Correction","value":"{{resumeValue}}"}]""";
        return $$"""{"ranking": {{ranking}}, "resume": {{resume}}}""";
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

    [Fact]
    public async Task DrainsQueueAndPostsVerdictWithoutTailoringWhenBelowPassmark()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson(NextJob(11, "fp11"));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        llmHandler.RespondJson(LlmContent(WeakVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(4, coreHandler.Requests.Count);
        Assert.Single(llmHandler.Requests);

        Assert.Equal("/ai/memory", coreHandler.Requests[0].RequestUri!.AbsolutePath);
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
        Assert.Equal("json_schema", chat.GetProperty("response_format").GetProperty("type").GetString());
        Assert.False(chat.GetProperty("stream").GetBoolean());

        var messages = chat.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Contains("Judge this posting.", messages[0].GetProperty("content").GetString());
        Assert.Contains("\"title\":\"C#\"", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Contains("Senior .NET role with Angular.", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task PromotingVerdictRunsCall2AndPostsDelta()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson(NextJob(21));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        llmHandler.RespondJson(LlmContent(ValidVerdict));
        llmHandler.RespondJson(LlmContent(ValidDelta));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(2, llmHandler.Requests.Count);

        var verdict = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.True(verdict.TryGetProperty("delta", out var delta));
        Assert.Equal("Senior .NET Engineer", delta.GetProperty("texts").GetProperty("title").GetString());

        var tailor_chat = JsonDocument.Parse(llmHandler.Bodies[1]).RootElement;
        Assert.Equal("resume_tailoring_delta", tailor_chat.GetProperty("response_format")
            .GetProperty("json_schema").GetProperty("name").GetString());

        var tailor_messages = tailor_chat.GetProperty("messages").EnumerateArray().ToArray();
        var system = tailor_messages[0].GetProperty("content").GetString()!;
        Assert.Contains("Tailor this resume.", system);
        Assert.Contains(PromptBuilder.InventoryLabel, system);
        Assert.Contains("#douran", system);
        Assert.Contains(PromptBuilder.SelectionLabel, system);
        Assert.Contains("Senior .NET role with Angular.", tailor_messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Call2ModelOutputFailureRetriesThenPostsVerdictAlone()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson(NextJob(22));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
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
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson(NextJob(23));
        llmHandler.RespondJson(LlmContent(ValidVerdict));
        llmHandler.RespondNetworkError();

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitLlmUnavailable, exit);
        Assert.Equal(2, coreHandler.Requests.Count);
        Assert.Empty(coreHandler.Bodies);
    }

    [Fact]
    public async Task ErrorVerdictSkipsCall2()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson(NextJob(24));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
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
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson(NextJob(12));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
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
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson(NextJob(13));
        llmHandler.RespondNetworkError();

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitLlmUnavailable, exit);
        Assert.Equal(2, coreHandler.Requests.Count);
        Assert.Empty(coreHandler.Bodies);
    }

    [Fact]
    public async Task ModelHttp500AbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson(NextJob(14));
        llmHandler.RespondJson("boom", HttpStatusCode.InternalServerError);

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitLlmUnavailable, exit);
        Assert.Equal(2, coreHandler.Requests.Count);
    }

    [Fact]
    public async Task Verdict404ContinuesWithNextJob()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson(NextJob(15));
        coreHandler.RespondJson("""{"error": "not found"}""", HttpStatusCode.NotFound);
        coreHandler.RespondJson(NextJob(16, passmark: 90));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        llmHandler.RespondJson(LlmContent(ValidVerdict));
        llmHandler.RespondJson(LlmContent(ValidDelta));
        llmHandler.RespondJson(LlmContent(WeakVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(6, coreHandler.Requests.Count);
        Assert.Equal(3, llmHandler.Requests.Count);
        var lastVerdict = JsonDocument.Parse(coreHandler.Bodies[1]).RootElement;
        Assert.Equal(16, lastVerdict.GetProperty("jobId").GetInt64());
    }

    [Fact]
    public async Task Verdict400AbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson(NextJob(17));
        coreHandler.RespondJson("""{"error": "validation", "message": "bad"}""", HttpStatusCode.BadRequest);
        llmHandler.RespondJson(LlmContent(WeakVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitCoreAbort, exit);
    }

    [Fact]
    public async Task NextNon200AbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson("oops", HttpStatusCode.InternalServerError);

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitCoreAbort, exit);
        Assert.Empty(llmHandler.Requests);
    }

    [Fact]
    public async Task NextNetworkErrorAbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondNetworkError("core down");

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitCoreAbort, exit);
        Assert.Empty(llmHandler.Requests);
    }

    [Fact]
    public async Task MemorySnapshotFailureAbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson("oops", HttpStatusCode.InternalServerError);

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitCoreAbort, exit);
        Assert.Single(coreHandler.Requests);
        Assert.Equal("/ai/memory", coreHandler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Empty(llmHandler.Requests);
    }

    [Fact]
    public async Task UsageAndFinishReasonAreCarriedThrough()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson(NextJob(41));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        llmHandler.RespondJson(LlmContentWithMeta(ValidVerdict, 1500, 220, "stop"));
        llmHandler.RespondJson(LlmContentWithMeta(ValidDelta, 3000, 350, "stop"));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        var verdict = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.Equal("Match", verdict.GetProperty("verdict").GetString());
    }

    [Fact]
    public async Task FinishReasonLengthWithInvalidOutputStillYieldsErrorVerdict()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(MemorySnapshotJson());
        coreHandler.RespondJson(NextJob(42));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        llmHandler.RespondJson(LlmContentWithMeta("oops not json", 100, 4000, "length"));
        llmHandler.RespondJson(LlmContentWithMeta("""{"verdict": "Match"}""", 100, 4000, "length"));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(2, llmHandler.Requests.Count);
        var verdict = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.Equal("Error", verdict.GetProperty("verdict").GetString());
        Assert.False(verdict.TryGetProperty("delta", out _));
    }

    [Fact]
    public async Task MemorySnapshotIsFetchedOnceAndInjectedIntoBothCalls()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(MemorySnapshotJson(
            rankingField: "remote_only", rankingValue: "remote roles only",
            resumeField: "summary", resumeValue: "lead with backend scale"));
        coreHandler.RespondJson(NextJob(31));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        llmHandler.RespondJson(LlmContent(ValidVerdict));
        llmHandler.RespondJson(LlmContent(ValidDelta));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(4, coreHandler.Requests.Count);
        Assert.Single(coreHandler.Requests, request => request.RequestUri!.AbsolutePath == "/ai/memory");

        var judge_system = JsonDocument.Parse(llmHandler.Bodies[0]).RootElement
            .GetProperty("messages").EnumerateArray().ToArray()[0].GetProperty("content").GetString()!;
        Assert.Contains(PromptBuilder.RankingMemoryLabel, judge_system);
        Assert.Contains("remote_only", judge_system);
        Assert.Contains("remote roles only", judge_system);
        Assert.DoesNotContain("lead with backend scale", judge_system);

        var tailor_system = JsonDocument.Parse(llmHandler.Bodies[1]).RootElement
            .GetProperty("messages").EnumerateArray().ToArray()[0].GetProperty("content").GetString()!;
        Assert.Contains(PromptBuilder.ResumeMemoryLabel, tailor_system);
        Assert.Contains("lead with backend scale", tailor_system);
        Assert.DoesNotContain("remote roles only", tailor_system);
    }
}
