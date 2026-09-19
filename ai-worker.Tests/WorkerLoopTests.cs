using System.Net;
using System.Text.Json;

namespace AiWorker.Tests;

public sealed class WorkerLoopTests
{
    private const string Rubric = "Judge this posting. Keywords: {{keywords}}.";

    private static LlmOptions Options()
    {
        return new LlmOptions
        {
            BaseUrl = "http://llm.test/v1",
            Model = "test-model",
            Core = "http://core.test",
            CoreApiKey = "worker-key",
            Rubric = Rubric,
            Temperature = 0.2,
            Seed = 42,
        };
    }

    private static WorkerLoop Loop(FakeHandler coreHandler, FakeHandler llmHandler, LlmOptions? options = null)
    {
        options ??= Options();
        var core = new CoreClient(FakeHttp.Client(coreHandler), options.CoreApiKey);
        var llm = new LlmClient(FakeHttp.Client(llmHandler, "http://llm.test/v1/"), options);
        return new WorkerLoop(core, llm, new PromptBuilder(options.Rubric));
    }

    private static string NextJob(long id, string fingerprint = "fp")
    {
        return $$"""
        {"empty": false, "jobId": {{id}}, "content": "Senior .NET role with Angular.",
         "resume": "RESUME TEXT", "keywords": [{"category": "tech", "score": 120, "title": "C#"}],
         "fingerprint": "{{fingerprint}}", "settings": {"aipassmark": 60}
        }
        """;
    }

    private static string NextEmpty()
    {
        return """{"empty": true}""";
    }

    private static string LlmContent(string verdictJson)
    {
        var embedded = JsonSerializer.Serialize(verdictJson);
        return "{\"choices\": [{\"message\": {\"role\": \"assistant\", \"content\": " + embedded + "}}]}";
    }

    private const string ValidVerdict =
        """{"relevance": 82, "verdict": "Match", "reason": "ok", "seniority": "Senior", "salary_min": 60000, "salary_max": 80000, "currency": "EUR", "period": "Year", "work_model": "Hybrid", "contract": "Permanent", "experience_years": 5, "skills": [".NET"]}""";

    [Fact]
    public async Task DrainsQueueAndPostsVerdict()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(NextJob(11, "fp11"));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        llmHandler.RespondJson(LlmContent(ValidVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(3, coreHandler.Requests.Count);
        Assert.Single(llmHandler.Requests);

        var verdict = JsonDocument.Parse(coreHandler.Bodies[0]).RootElement;
        Assert.Equal(11, verdict.GetProperty("jobId").GetInt64());
        Assert.Equal(82, verdict.GetProperty("relevance").GetInt32());
        Assert.Equal("Match", verdict.GetProperty("verdict").GetString());
        Assert.Equal("fp11", verdict.GetProperty("fingerprint").GetString());

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
    public async Task TwoBadModelOutputsPostErrorVerdict()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
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
        coreHandler.RespondJson(NextJob(13));
        llmHandler.RespondNetworkError();

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitLlmUnavailable, exit);
        Assert.Single(coreHandler.Requests);
        Assert.Empty(coreHandler.Bodies);
    }

    [Fact]
    public async Task ModelHttp500AbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(NextJob(14));
        llmHandler.RespondJson("boom", HttpStatusCode.InternalServerError);

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitLlmUnavailable, exit);
        Assert.Single(coreHandler.Requests);
    }

    [Fact]
    public async Task Verdict404ContinuesWithNextJob()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(NextJob(15));
        coreHandler.RespondJson("""{"error": "not found"}""", HttpStatusCode.NotFound);
        coreHandler.RespondJson(NextJob(16));
        coreHandler.RespondJson("{}");
        coreHandler.RespondJson(NextEmpty());
        llmHandler.RespondJson(LlmContent(ValidVerdict));
        llmHandler.RespondJson(LlmContent(ValidVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitOk, exit);
        Assert.Equal(5, coreHandler.Requests.Count);
        Assert.Equal(2, llmHandler.Requests.Count);
        var lastVerdict = JsonDocument.Parse(coreHandler.Bodies[1]).RootElement;
        Assert.Equal(16, lastVerdict.GetProperty("jobId").GetInt64());
    }

    [Fact]
    public async Task Verdict400AbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
        coreHandler.RespondJson(NextJob(17));
        coreHandler.RespondJson("""{"error": "validation", "message": "bad"}""", HttpStatusCode.BadRequest);
        llmHandler.RespondJson(LlmContent(ValidVerdict));

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitCoreAbort, exit);
    }

    [Fact]
    public async Task NextNon200AbortsRun()
    {
        var coreHandler = new FakeHandler();
        var llmHandler = new FakeHandler();
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
        coreHandler.RespondNetworkError("core down");

        var exit = await Loop(coreHandler, llmHandler).RunAsync(CancellationToken.None);

        Assert.Equal(WorkerLoop.ExitCoreAbort, exit);
        Assert.Empty(llmHandler.Requests);
    }
}
