namespace AiWorker.Tests;

public sealed class PromptBuilderTests
{
    private const string Rubric = "RUBRIC HEAD. Weight keywords: {{keywords}}. RUBRIC TAIL.";
    private const string RubricTailor = "TAILOR HEAD. Weight keywords: {{keywords}}. TAILOR TAIL.";

    private static PromptBuilder Builder() => new(Rubric, RubricTailor);

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
        var prompt = Builder().Compose(Payload());
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
        var prompt = Builder().Compose(Payload());

        Assert.DoesNotContain("{{keywords}}", prompt.System, StringComparison.Ordinal);
        Assert.Contains("\"title\":\"C#\"", prompt.System);
    }

    [Fact]
    public void SystemPromptIsStableAcrossJobs()
    {
        var builder = Builder();
        var first = builder.Compose(Payload(content: "job one text")).System;
        var second = builder.Compose(Payload(content: "a completely different posting")).System;

        Assert.Equal(first, second);
    }

    [Fact]
    public void OverLongJobTextIsTailTruncated()
    {
        var huge = new string('x', 400_000) + " UNIQUE TAIL MARKER";
        var prompt = Builder().Compose(Payload(content: huge, resume: "short"));

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
        var prompt = Builder().Compose(Payload(content: text));

        Assert.Contains(text, prompt.User, StringComparison.Ordinal);
    }

    [Fact]
    public void TailorBlocksAreInStableOrder()
    {
        var payload = Payload();
        payload.Options = "{\"Length\": 1, \"Keys\": {\"DOTNET\": []}}";
        payload.Inventory =
        [
            new InventoryItem { Id = "title", Type = "slot", Text = "Senior Full Stack Software Developer" },
            new InventoryItem { Id = "#douran", Type = "block", Keys = ["key-java"], Text = "Douran" },
            new InventoryItem { Id = "#douran li:nth-child(2)", Type = "bullet", Text = "Built services." },
        ];

        var prompt = Builder().ComposeTailor(payload);
        var merged = prompt.System + "\n" + prompt.User;

        var rubric = merged.IndexOf("TAILOR HEAD", StringComparison.Ordinal);
        var memory = merged.IndexOf(PromptBuilder.ResumeMemoryLabel, StringComparison.Ordinal);
        var inventory = merged.IndexOf(PromptBuilder.InventoryLabel, StringComparison.Ordinal);
        var selection = merged.IndexOf(PromptBuilder.SelectionLabel, StringComparison.Ordinal);
        var job = merged.IndexOf(PromptBuilder.JobLabel, StringComparison.Ordinal);

        Assert.True(rubric >= 0 && memory > rubric, "resume-memory slot must follow the rubric");
        Assert.True(inventory > memory, "inventory must follow the memory slot");
        Assert.True(selection > inventory, "current selection must follow the inventory");
        Assert.True(job > selection, "job description must come last");

        Assert.DoesNotContain("{{keywords}}", prompt.System, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"#douran li:nth-child(2)\"", prompt.System);
        Assert.Contains(payload.Options!, prompt.System, StringComparison.Ordinal);
    }

    [Fact]
    public void TailorSystemPromptIsStableAcrossJobs()
    {
        var builder = Builder();
        var payload = Payload(content: "job one text");
        payload.Inventory = [new InventoryItem { Id = "title", Type = "slot", Text = "Title" }];
        var other = Payload(content: "a completely different posting");
        other.Inventory = payload.Inventory;

        Assert.Equal(builder.ComposeTailor(payload).System, builder.ComposeTailor(other).System);
    }

    [Fact]
    public void TailorOverLongJobTextIsTailTruncated()
    {
        var huge = new string('x', 400_000) + " UNIQUE TAIL MARKER";
        var payload = Payload(content: huge, resume: "short");
        payload.Inventory = [new InventoryItem { Id = "title", Type = "slot", Text = "Title" }];

        var prompt = Builder().ComposeTailor(payload);

        var content = prompt.User[(prompt.User.IndexOf(PromptBuilder.JobLabel, StringComparison.Ordinal)
            + PromptBuilder.JobLabel.Length)..].TrimStart();
        var expectedChars = Math.Max(PromptBuilder.MaxContextTokens - PromptBuilder.Estimate(prompt.System), 0)
            * PromptBuilder.CharsPerToken;

        Assert.True(content.Length <= expectedChars, $"JD {content.Length} chars exceeds budget {expectedChars}");
        Assert.DoesNotContain("UNIQUE TAIL MARKER", content, StringComparison.Ordinal);
    }
}
