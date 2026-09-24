namespace AiWorker.Tests;

public sealed class LlmOptionsTests
{
    private static LlmOptions Valid()
    {
        return new LlmOptions
        {
            BaseUrl = "http://llm.test/v1",
            Model = "test-model",
            Core = "http://core.test",
            CoreApiKey = "worker-key",
            Fixed = "fixed",
            Rubric = "ranking",
            RubricTailor = "tailoring",
        };
    }

    [Fact]
    public void ValidateRequiresFixed()
    {
        var options = Valid();
        options.Fixed = " ";
        Assert.Equal("Llm:Fixed is required", options.Validate());
    }

    [Fact]
    public void ValidatePassesWhenFixedAndRubricsAreSet()
    {
        Assert.Null(Valid().Validate());
    }
}
