namespace AiWorker.Tests;

public sealed class PromptBuilderTests
{
    private const string Rubric = "RUBRIC HEAD. Weight keywords: {{keywords}}. RUBRIC TAIL.";
    private const string RubricTailor = "TAILOR HEAD. Weight keywords: {{keywords}}. TAILOR TAIL.";

    private static PromptBuilder Builder() => new(Rubric, RubricTailor);

    private static int BudgetTokens(string system)
    {
        return Math.Max(PromptBuilder.MaxContextTokens
            - PromptBuilder.DefaultCompletionReserveTokens
            - PromptBuilder.EstimateSlackTokens
            - PromptBuilder.Estimate(system), 0);
    }

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
        var expectedChars = BudgetTokens(prompt.System) * PromptBuilder.CharsPerToken;

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
        var expectedChars = BudgetTokens(prompt.System) * PromptBuilder.CharsPerToken;

        Assert.True(content.Length <= expectedChars, $"JD {content.Length} chars exceeds budget {expectedChars}");
        Assert.DoesNotContain("UNIQUE TAIL MARKER", content, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyMemoryKeepsPlaceholder()
    {
        var prompt = Builder().Compose(Payload());
        Assert.Contains(PromptBuilder.NoMemoryText, prompt.System, StringComparison.Ordinal);

        prompt = Builder().Compose(Payload(), []);
        Assert.Contains(PromptBuilder.NoMemoryText, prompt.System, StringComparison.Ordinal);

        var tailor = Builder().ComposeTailor(Payload());
        Assert.Contains(PromptBuilder.NoMemoryText, tailor.System, StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryRowsAreInjectedAsDataLines()
    {
        var ranking = new List<MemorySnapshotRow>
        {
            new() { Domain = "*", FieldKey = "remote_only", Kind = "Tip", Value = "remote roles only", Note = null },
            new() { Domain = "linkedin.com", FieldKey = "relocation", Kind = "Correction", Value = "needs visa", Note = "2026 batch" },
        };

        var prompt = Builder().Compose(Payload(), ranking);
        var start = prompt.System.IndexOf(PromptBuilder.RankingMemoryLabel, StringComparison.Ordinal)
            + PromptBuilder.RankingMemoryLabel.Length;
        var end = prompt.System.IndexOf(PromptBuilder.ResumeLabel, StringComparison.Ordinal);
        var block = prompt.System[start..end].Trim();

        Assert.DoesNotContain(PromptBuilder.NoMemoryText, block);
        var lines = block.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("{\"domain\":\"*\",\"fieldKey\":\"remote_only\"", lines[0]);
        Assert.StartsWith("{\"domain\":\"linkedin.com\",\"fieldKey\":\"relocation\"", lines[1]);
        Assert.Contains("\"value\":\"needs visa\"", lines[1]);
        Assert.Contains("\"note\":\"2026 batch\"", lines[1]);

        var resume_memory = new List<MemorySnapshotRow>
        {
            new() { Domain = "*", FieldKey = "summary", Kind = "Tip", Value = "lead with backend scale" },
        };
        var tailor = Builder().ComposeTailor(Payload(), resume_memory);
        Assert.Contains("\"fieldKey\":\"summary\"", tailor.System);
        Assert.DoesNotContain("\"note\"", tailor.System.Split(PromptBuilder.ResumeMemoryLabel)[1]
            .Split(PromptBuilder.InventoryLabel)[0], StringComparison.Ordinal);
    }

    [Fact]
    public void OverLongJobTextReportsTruncationMetrics()
    {
        var huge = new string('x', 400_000);
        var prompt = Builder().Compose(Payload(content: huge, resume: "short"));

        var expectedChars = BudgetTokens(prompt.System) * PromptBuilder.CharsPerToken;

        Assert.Equal(huge.Length, prompt.ContentOriginalChars);
        Assert.Equal(huge.Length - expectedChars, prompt.ContentTruncatedChars);
        Assert.True(prompt.ContentTruncatedChars > 0);
    }

    [Fact]
    public void CustomCompletionReserveShrinksContentBudget()
    {
        var huge = new string('x', 400_000);
        var prompt = new PromptBuilder(Rubric, RubricTailor, 4096)
            .Compose(Payload(content: huge, resume: "short"));

        var content = prompt.User[(prompt.User.IndexOf(PromptBuilder.JobLabel, StringComparison.Ordinal)
            + PromptBuilder.JobLabel.Length)..].TrimStart();
        var expectedChars = Math.Max(PromptBuilder.MaxContextTokens
            - 4096 - PromptBuilder.EstimateSlackTokens
            - PromptBuilder.Estimate(prompt.System), 0) * PromptBuilder.CharsPerToken;

        Assert.True(content.Length <= expectedChars, $"JD {content.Length} chars exceeds budget {expectedChars}");
    }

    [Fact]
    public void ShortJobTextReportsZeroTruncation()
    {
        var prompt = Builder().Compose(Payload(content: "Short posting."));

        Assert.Equal(0, prompt.ContentTruncatedChars);
    }

    [Fact]
    public void OversizedMemoryListReportsDroppedRows()
    {
        var rows = new List<MemorySnapshotRow>();
        for (var i = 0; i < 100; i++)
            rows.Add(new MemorySnapshotRow
            {
                Domain = "*",
                FieldKey = $"key_{i}",
                Kind = "Tip",
                Value = new string('v', 200),
            });

        PromptBuilder.MemoryBlock(rows, out var dropped, out var total);
        Assert.Equal(100, total);
        Assert.True(dropped > 0);

        var prompt = Builder().Compose(Payload(), rows);
        Assert.Equal(dropped, prompt.DroppedMemoryRows);
        Assert.Equal(total, prompt.TotalMemoryRows);
    }

    [Fact]
    public void MemoryBlockKeepsWholeRowsWithinTokenCap()
    {
        var rows = new List<MemorySnapshotRow>();
        for (var i = 0; i < 100; i++)
            rows.Add(new MemorySnapshotRow
            {
                Domain = "*",
                FieldKey = $"key_{i}",
                Kind = "Tip",
                Value = new string('v', 200),
            });

        var block = PromptBuilder.MemoryBlock(rows, out _, out _);

        Assert.DoesNotContain(PromptBuilder.NoMemoryText, block);
        Assert.True(block.Length <= PromptBuilder.MemoryTokenCap * PromptBuilder.CharsPerToken,
            $"memory block {block.Length} chars exceeds the cap");
        foreach (var line in block.Split('\n'))
            Assert.StartsWith("{\"domain\":\"*\",\"fieldKey\":\"key_", line);
        Assert.StartsWith("{\"domain\":\"*\",\"fieldKey\":\"key_0\"", block);
    }
}
