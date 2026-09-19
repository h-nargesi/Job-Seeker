namespace AiWorker.Tests;

public sealed class PromptBuilderTests
{
    private const string Rubric = "RUBRIC HEAD. Weight keywords: {{keywords}}. RUBRIC TAIL.";

    private static AiNextPayload Payload(string content = "Senior .NET role with Angular.", string? resume = "RESUME TEXT")
    {
        return new AiNextPayload
        {
            Empty = false,
            JobId = 7,
            Content = content,
            Resume = resume,
            Fingerprint = "abc",
            Keywords =
            [
                new JobKeyword { Category = "tech", Score = 120, Title = "C#" },
                new JobKeyword { Category = "web", Score = 60, Title = "Angular" },
            ],
        };
    }

    [Fact]
    public void BlocksAreInStableOrder()
    {
        var prompt = new PromptBuilder(Rubric).Compose(Payload());
        var keywords = PromptBuilder.KeywordsJson(Payload().Keywords);
        var merged = prompt.System + "\n" + prompt.User;

        var rubric = merged.IndexOf("RUBRIC HEAD", StringComparison.Ordinal);
        var keywordPosition = merged.IndexOf(keywords, StringComparison.Ordinal);
        var memory = merged.IndexOf(PromptBuilder.RankingMemoryLabel, StringComparison.Ordinal);
        var resume = merged.IndexOf(PromptBuilder.ResumeLabel, StringComparison.Ordinal);
        var job = merged.IndexOf(PromptBuilder.JobLabel, StringComparison.Ordinal);

        Assert.True(rubric >= 0 && keywordPosition > rubric, "keywords must follow the rubric head");
        Assert.True(memory > keywordPosition, "ranking-memory slot must follow keywords");
        Assert.True(resume > memory, "resume must follow the memory slot");
        Assert.True(job > resume, "job description must come last");
    }

    [Fact]
    public void KeywordsPlaceholderIsInjected()
    {
        var prompt = new PromptBuilder(Rubric).Compose(Payload());

        Assert.DoesNotContain("{{keywords}}", prompt.System, StringComparison.Ordinal);
        Assert.Contains("\"title\":\"C#\"", prompt.System);
    }

    [Fact]
    public void SystemPromptIsStableAcrossJobs()
    {
        var builder = new PromptBuilder(Rubric);
        var first = builder.Compose(Payload(content: "job one text")).System;
        var second = builder.Compose(Payload(content: "a completely different posting")).System;

        Assert.Equal(first, second);
    }

    [Fact]
    public void OverLongJobTextIsTailTruncated()
    {
        var huge = new string('x', 400_000) + " UNIQUE TAIL MARKER";
        var prompt = new PromptBuilder(Rubric).Compose(Payload(content: huge, resume: "short"));

        var content = prompt.User[(prompt.User.IndexOf(PromptBuilder.JobLabel, StringComparison.Ordinal)
            + PromptBuilder.JobLabel.Length)..].TrimStart();
        var expectedChars = Math.Max(PromptBuilder.MaxContextTokens - PromptBuilder.Estimate(prompt.System), 0)
            * PromptBuilder.CharsPerToken;

        Assert.True(content.Length <= expectedChars, $"JD {content.Length} chars exceeds budget {expectedChars}");
        Assert.StartsWith(new string('x', 100), content, StringComparison.Ordinal);
        Assert.DoesNotContain("UNIQUE TAIL MARKER", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ShortJobTextIsNotTruncated()
    {
        var text = "Short posting.";
        var prompt = new PromptBuilder(Rubric).Compose(Payload(content: text));

        Assert.Contains(text, prompt.User, StringComparison.Ordinal);
    }
}
