namespace AiWorker.Tests;

public sealed class PromptBuilderTests
{
    private const string Rubric = "RANKING TASK BODY.";
    private const string RubricTailor = "TAILORING TASK BODY.";

    private static PromptBuilder Builder() => new(Rubric, RubricTailor);

    private static AiContext Context(
        string resume = "RESUME TEXT",
        IReadOnlyList<MemorySnapshotRow>? ranking = null,
        IReadOnlyList<MemorySnapshotRow>? resumeMemory = null)
    {
        return new AiContext
        {
            RankingMemory = [.. (ranking ?? [])],
            ResumeMemory = [.. (resumeMemory ?? [])],
            Keywords =
            [
                new JobKeyword { Category = "tech", Score = 120, Title = "C#" },
                new JobKeyword { Category = "web", Score = 60, Title = "Angular" },
            ],
            Resume = resume,
            Inventory =
            [
                new InventoryItem { Id = "title", Type = "slot", Text = "Senior Full Stack Software Developer" },
                new InventoryItem { Id = "#douran", Type = "block", Keys = ["key-java"], Text = "Douran" },
            ],
            AiPassmark = 60,
            ContextVersion = "v1",
        };
    }

    private static PromptBuilder.Prompt Rank(PromptBuilder builder, PromptBuilder.Trunk trunk,
        AiNextPayload payload, int passmark = 60)
    {
        return builder.Compose(trunk, payload, builder.PrepareJob(trunk, payload), passmark);
    }

    private static AiNextPayload Payload(string content = "Senior .NET role with Angular.", string? options = null)
    {
        return new AiNextPayload
        {
            Empty = false,
            JobId = 7,
            Content = content,
            Fingerprint = "abc",
            Options = options,
            ContextVersion = "v1",
        };
    }

    [Fact]
    public void TrunkSectionsAreInStableOrder()
    {
        var trunk = Builder().BuildTrunk(Context());

        Assert.StartsWith("The labeled sections below are the candidate's fixed context", trunk.System, StringComparison.Ordinal);
        Assert.Contains("Use only the sections", trunk.System, StringComparison.Ordinal);
        var preamble = trunk.System.IndexOf("instructions to you.", StringComparison.Ordinal);
        var keywords = trunk.System.IndexOf(PromptBuilder.KeywordsLabel, StringComparison.Ordinal);
        var ranking = trunk.System.IndexOf(PromptBuilder.RankingMemoryLabel, StringComparison.Ordinal);
        var resume_memory = trunk.System.IndexOf(PromptBuilder.ResumeMemoryLabel, StringComparison.Ordinal);
        var resume = trunk.System.IndexOf(PromptBuilder.ResumeLabel, StringComparison.Ordinal);
        var inventory = trunk.System.IndexOf(PromptBuilder.InventoryLabel, StringComparison.Ordinal);

        Assert.True(preamble >= 0 && keywords > preamble, "keywords must follow the preamble");
        Assert.True(ranking > keywords, "ranking memory must follow keywords");
        Assert.True(resume_memory > ranking, "resume memory must follow ranking memory");
        Assert.True(resume > resume_memory, "resume must follow resume memory");
        Assert.True(inventory > resume, "inventory must come last");
        Assert.Contains("\"title\":\"C#\"", trunk.System);
        Assert.Contains("\"id\":\"#douran\"", trunk.System);
    }

    [Fact]
    public void TrunkIsStableAcrossIdenticalContexts()
    {
        var builder = Builder();

        Assert.Equal(builder.BuildTrunk(Context()).System, builder.BuildTrunk(Context()).System);
    }

    [Fact]
    public void TrunkExposesHashAndTokenEstimate()
    {
        var trunk = Builder().BuildTrunk(Context());

        Assert.Equal(RunReport.ShortHash(trunk.System), trunk.Hash);
        Assert.Equal(trunk.System.Length / PromptBuilder.CharsPerToken, trunk.EstTokens);
    }

    [Fact]
    public void VerdictUserMessagePlacesTaskAfterJob()
    {
        var builder = Builder();
        var trunk = builder.BuildTrunk(Context());
        var prompt = Rank(builder, trunk, Payload());

        Assert.Equal(trunk.System, prompt.System);
        Assert.StartsWith(PromptBuilder.JobLabel, prompt.User, StringComparison.Ordinal);
        var job = prompt.User.IndexOf(PromptBuilder.JobLabel, StringComparison.Ordinal);
        var task = prompt.User.IndexOf(PromptBuilder.TaskRankingLabel, StringComparison.Ordinal);
        Assert.True(task > job, "task must follow the job posting");
        Assert.EndsWith(Rubric, prompt.User, StringComparison.Ordinal);
        var threshold = prompt.User.IndexOf(PromptBuilder.ApplyThreshold(60), StringComparison.Ordinal);
        Assert.True(threshold > task, "passmark must follow the ranking task label");
        Assert.True(prompt.User.IndexOf(Rubric, StringComparison.Ordinal) > threshold,
            "rubric must follow the passmark line");
    }

    [Fact]
    public void RankingPassmarkIsAbsentFromTheTailorUserMessage()
    {
        var builder = Builder();
        var trunk = builder.BuildTrunk(Context());
        var payload = Payload();
        var job = builder.PrepareJob(trunk, payload);

        var ranking = builder.Compose(trunk, payload, job, 75);
        var tailor = builder.ComposeTailor(trunk, payload, job);

        Assert.Contains(PromptBuilder.ApplyThreshold(75), ranking.User, StringComparison.Ordinal);
        Assert.DoesNotContain(PromptBuilder.ApplyThreshold(75), tailor.User, StringComparison.Ordinal);
        Assert.DoesNotContain("Apply threshold:", tailor.User, StringComparison.Ordinal);
    }

    [Fact]
    public void TailorUserMessagePlacesSelectionBetweenJobAndTask()
    {
        var builder = Builder();
        var trunk = builder.BuildTrunk(Context());
        var payload = Payload(options: "{\"Length\": 1, \"Keys\": {\"DOTNET\": []}}");
        var prompt = builder.ComposeTailor(trunk, payload, builder.PrepareJob(trunk, payload));

        var job = prompt.User.IndexOf(PromptBuilder.JobLabel, StringComparison.Ordinal);
        var selection = prompt.User.IndexOf(PromptBuilder.SelectionLabel, StringComparison.Ordinal);
        var task = prompt.User.IndexOf(PromptBuilder.TaskTailoringLabel, StringComparison.Ordinal);
        Assert.True(job == 0 && selection > job, "selection must follow the job posting");
        Assert.True(task > selection, "task must follow the selection");
        Assert.EndsWith(RubricTailor, prompt.User, StringComparison.Ordinal);
        Assert.Contains("""{"keys":["DOTNET"],"included":[],"notIncluded":[],"length":1}""", prompt.User, StringComparison.Ordinal);
        Assert.DoesNotContain(PromptBuilder.SelectionLabel, prompt.User[..selection], StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSelectionRendersEmptyObject()
    {
        var builder = Builder();
        var trunk = builder.BuildTrunk(Context());
        var prompt = builder.ComposeTailor(trunk, Payload(), builder.PrepareJob(trunk, Payload()));

        var block = prompt.User.Split(PromptBuilder.SelectionLabel + "\n")[1].Split("\n\n")[0];
        Assert.Equal(PromptBuilder.EmptySelection, block);
    }

    [Fact]
    public void SelectionProjectionDropsNullKeysKeepsEmptyArraysAndPascalCase()
    {
        var raw = """
            {
              "Version": 42,
              "HumanEdited": false,
              "Length": 2,
              "JobTitle": "Senior Full Stack Software Developer",
              "InputData": { "BACK_END_EXP": 14 },
              "Keys": { "DOTNET": [], "JAVA": null, "SQL": ["tsql"] },
              "PageBreak": [],
              "NotIncluded": ["#web-sites"],
              "Included": ["#douran"],
              "Elements": { "PHONE": true }
            }
            """;

        var projected = PromptBuilder.ProjectSelection(raw);

        Assert.Equal("""{"keys":["DOTNET","SQL"],"included":["#douran"],"notIncluded":["#web-sites"],"length":2}""",
            projected);
        Assert.DoesNotContain("JobTitle", projected, StringComparison.Ordinal);
        Assert.DoesNotContain("JAVA", projected, StringComparison.Ordinal);
    }

    [Fact]
    public void UnparseableSelectionFallsBackToEmptyProjection()
    {
        Assert.Equal(PromptBuilder.EmptySelection, PromptBuilder.ProjectSelection("not-json"));
        Assert.Equal(PromptBuilder.EmptySelection, PromptBuilder.ProjectSelection("[1,2]"));
        Assert.Equal(PromptBuilder.EmptySelection, PromptBuilder.Selection(Payload()));
    }

    [Fact]
    public void RubricsAreUsedVerbatimWithoutSubstitution()
    {
        var builder = Builder();
        var trunk = builder.BuildTrunk(Context());
        var payload = Payload();
        var job = builder.PrepareJob(trunk, payload);

        Assert.Contains(Rubric, builder.Compose(trunk, payload, job, 60).User, StringComparison.Ordinal);
        Assert.Contains(RubricTailor, builder.ComposeTailor(trunk, payload, job).User, StringComparison.Ordinal);
        Assert.DoesNotContain("{{keywords}}", trunk.System + builder.Compose(trunk, payload, job, 60).User);
    }

    [Fact]
    public void JobPostingIsTruncatedOnceAndSharedByteIdentically()
    {
        var huge = new string('x', 400_000) + " UNIQUE TAIL MARKER";
        var builder = Builder();
        var trunk = builder.BuildTrunk(Context(resume: "short"));
        var payload = Payload(content: huge);
        var job = builder.PrepareJob(trunk, payload);
        var verdict = builder.Compose(trunk, payload, job, 60);
        var tailor = builder.ComposeTailor(trunk, payload, job);

        var expected_chars = job.BudgetTokens * PromptBuilder.CharsPerToken;
        Assert.True(job.Truncated.Length <= expected_chars,
            $"JD {job.Truncated.Length} chars exceeds budget {expected_chars}");
        Assert.StartsWith(new string('x', 100), job.Truncated, StringComparison.Ordinal);
        Assert.DoesNotContain("UNIQUE TAIL MARKER", job.Truncated, StringComparison.Ordinal);

        var verdict_jd = ExtractJobPosting(verdict.User);
        var tailor_jd = ExtractJobPosting(tailor.User);
        Assert.Equal(job.Truncated, verdict_jd);
        Assert.Equal(job.Truncated, tailor_jd);

        Assert.Equal(huge.Length, job.OriginalChars);
        Assert.Equal(huge.Length - job.Truncated.Length, job.TruncatedChars);
        Assert.True(job.TruncatedChars > 0);
    }

    [Fact]
    public void ShortJobTextIsNotTruncated()
    {
        var builder = Builder();
        var trunk = builder.BuildTrunk(Context());
        var payload = Payload(content: "Short posting.");
        var job = builder.PrepareJob(trunk, payload);

        Assert.Equal(0, job.TruncatedChars);
        Assert.Contains("Short posting.", builder.Compose(trunk, payload, job, 60).User, StringComparison.Ordinal);
    }

    [Fact]
    public void CacheablePrefixIsTrunkForCall1AndTrunkPlusJobForCall2()
    {
        var builder = Builder();
        var trunk = builder.BuildTrunk(Context());
        var payload = Payload();
        var job = builder.PrepareJob(trunk, payload);

        var verdict = builder.Compose(trunk, payload, job, 60);
        var tailor = builder.ComposeTailor(trunk, payload, job);

        Assert.Equal(trunk.EstTokens, verdict.CacheablePrefixTokens);
        Assert.Equal(trunk.EstTokens + PromptBuilder.Estimate(job.Truncated), tailor.CacheablePrefixTokens);
    }

    [Fact]
    public void CustomCompletionReserveShrinksContentBudget()
    {
        var huge = new string('x', 400_000);
        var builder = new PromptBuilder(Rubric, RubricTailor, 4096);
        var trunk = builder.BuildTrunk(Context(resume: "short"));
        var job = builder.PrepareJob(trunk, Payload(content: huge));

        var overhead = Math.Max(PromptBuilder.Estimate(Rubric),
            PromptBuilder.Estimate(RubricTailor) + PromptBuilder.Estimate(PromptBuilder.EmptySelection));
        var expected_budget = Math.Max(PromptBuilder.MaxContextTokens
            - 4096 - PromptBuilder.EstimateSlackTokens - trunk.EstTokens - overhead, 0);

        Assert.Equal(expected_budget, job.BudgetTokens);
        Assert.True(job.Truncated.Length <= expected_budget * PromptBuilder.CharsPerToken);
    }

    [Fact]
    public void EmptyMemoryKeepsPlaceholder()
    {
        var trunk = Builder().BuildTrunk(Context());

        Assert.Contains(PromptBuilder.NoMemoryText, trunk.System, StringComparison.Ordinal);

        var context = Context();
        context.RankingMemory = [];
        context.ResumeMemory = [];
        Assert.Contains(PromptBuilder.NoMemoryText, Builder().BuildTrunk(context).System, StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryRowsAreInjectedAsDataLinesUnderTheirLabels()
    {
        var ranking = new List<MemorySnapshotRow>
        {
            new() { Domain = "*", FieldKey = "remote_only", Kind = "Tip", Value = "remote roles only", Note = null },
            new() { Domain = "linkedin.com", FieldKey = "relocation", Kind = "Correction", Value = "needs visa", Note = "2026 batch" },
        };
        var resume_memory = new List<MemorySnapshotRow>
        {
            new() { Domain = "*", FieldKey = "summary", Kind = "Tip", Value = "lead with backend scale" },
        };

        var trunk = Builder().BuildTrunk(Context(ranking: ranking, resumeMemory: resume_memory));
        var ranking_block = LabelBlock(trunk.System, PromptBuilder.RankingMemoryLabel, PromptBuilder.ResumeMemoryLabel);
        var resume_block = LabelBlock(trunk.System, PromptBuilder.ResumeMemoryLabel, PromptBuilder.ResumeLabel);

        Assert.DoesNotContain(PromptBuilder.NoMemoryText, ranking_block);
        var lines = ranking_block.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("{\"domain\":\"*\",\"fieldKey\":\"remote_only\"", lines[0]);
        Assert.StartsWith("{\"domain\":\"linkedin.com\",\"fieldKey\":\"relocation\"", lines[1]);
        Assert.Contains("\"value\":\"needs visa\"", lines[1]);
        Assert.Contains("\"note\":\"2026 batch\"", lines[1]);

        Assert.Contains("\"fieldKey\":\"summary\"", resume_block);
        Assert.DoesNotContain("\"note\"", resume_block, StringComparison.Ordinal);
    }

    [Fact]
    public void OversizedMemoryListReportsDroppedRowsOnTheTrunk()
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

        var context = Context(ranking: rows, resumeMemory: rows);
        var trunk = Builder().BuildTrunk(context);

        Assert.Equal(200, trunk.TotalMemoryRows);
        Assert.True(trunk.DroppedMemoryRows > 0);
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

    private static string ExtractJobPosting(string user)
    {
        var after_label = user[(user.IndexOf(PromptBuilder.JobLabel, StringComparison.Ordinal)
            + PromptBuilder.JobLabel.Length)..].TrimStart();
        return after_label.Split("\n\n")[0];
    }

    private static string LabelBlock(string trunk, string label, string nextLabel)
    {
        var start = trunk.IndexOf(label, StringComparison.Ordinal) + label.Length;
        var end = trunk.IndexOf(nextLabel, StringComparison.Ordinal);
        return trunk[start..end].Trim();
    }
}
