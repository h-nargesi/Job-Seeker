using System.Text.Json;

namespace AiWorker.Tests;

public sealed class LlmClientTests
{
    [Fact]
    public void TimeoutComesFromOptions()
    {
        var http = FakeHttp.Client(new FakeHandler(), "http://llm.test/v1/");
        var options = new LlmOptions { TimeoutSeconds = 150 };

        _ = new LlmClient(http, options);

        Assert.Equal(TimeSpan.FromSeconds(150), http.Timeout);
    }

    [Fact]
    public async Task RequestBodyPinsSlotAndEnablesPromptCache()
    {
        var handler = new FakeHandler();
        handler.RespondJson("""{"choices": [{"message": {"role": "assistant", "content": "ok"}}]}""");
        var options = new LlmOptions { BaseUrl = "http://llm.test/v1", Model = "test-model", Slot = 3 };

        await new LlmClient(FakeHttp.Client(handler, "http://llm.test/v1/"), options)
            .CompleteAsync("system", "user", "{}", 0, CancellationToken.None);

        var body = JsonDocument.Parse(handler.Bodies[0]).RootElement;
        Assert.True(body.GetProperty("cache_prompt").GetBoolean());
        Assert.Equal(3, body.GetProperty("id_slot").GetInt32());
        Assert.Equal("system", body.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("user", body.GetProperty("messages")[1].GetProperty("role").GetString());
    }

    [Fact]
    public void ExtractParsesUsageAndFinishReason()
    {
        var body = """
            {"choices": [{"message": {"role": "assistant", "content": "{\"verdict\": \"Match\"}"},
             "finish_reason": "stop"}],
             "usage": {"prompt_tokens": 1200, "completion_tokens": 80}}
            """;

        var result = LlmClient.Extract(body);

        Assert.Equal("""{"verdict": "Match"}""", result.Content);
        Assert.Equal(1200, result.PromptTokens);
        Assert.Equal(80, result.CompletionTokens);
        Assert.Equal("stop", result.FinishReason);
        Assert.Equal(0, result.ElapsedMs);
    }

    [Fact]
    public void MissingUsageAndFinishReasonDegradeToNulls()
    {
        var body = """{"choices": [{"message": {"role": "assistant", "content": "hello"}}]}""";

        var result = LlmClient.Extract(body);

        Assert.Equal("hello", result.Content);
        Assert.Null(result.PromptTokens);
        Assert.Null(result.CompletionTokens);
        Assert.Null(result.FinishReason);
    }

    [Theory]
    [InlineData("""{"choices": [{"message": {"content": "hi"}}], "usage": "garbage"}""")]
    [InlineData("""{"choices": [{"message": {"content": "hi"}}], "usage": {"prompt_tokens": "x", "completion_tokens": null}}""")]
    [InlineData("""{"choices": [{"message": {"content": "hi"}}], "usage": {"prompt_tokens": 9999999999}}""")]
    public void MalformedUsageDegradesToNullsWithoutThrowing(string body)
    {
        var result = LlmClient.Extract(body);

        Assert.Equal("hi", result.Content);
        Assert.Null(result.PromptTokens);
        Assert.Null(result.CompletionTokens);
    }

    [Fact]
    public void MissingContentThrowsWithRawBodyAttached()
    {
        var body = """{"choices": [{"message": {"role": "assistant"}}]}""";

        var ex = Assert.Throws<ModelOutputException>(() => LlmClient.Extract(body));

        Assert.Equal(body, ex.Raw);
    }
}
