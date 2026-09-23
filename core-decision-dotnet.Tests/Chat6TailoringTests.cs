using System.Text.Json;

namespace Photon.JobSeeker.Tests;

public sealed class Chat6TailoringTests
{
    private static readonly Newtonsoft.Json.JsonSerializerSettings ResumeTextJson = new()
    {
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
        Converters =
        {
            new Newtonsoft.Json.Converters.StringEnumConverter(
                new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy(), false),
        },
    };

    private static ResumeInventory Inventory()
    {
        return new ResumeInventory(
        [
            new ResumeInventoryItem("title", ResumeInventoryItem.SlotType, [], "Senior Full Stack Software Developer"),
            new ResumeInventoryItem("summary", ResumeInventoryItem.SlotType, [], "Experienced software engineer…"),
            new ResumeInventoryItem("#douran", ResumeInventoryItem.BlockType, ["key-java"], "Douran Java role"),
            new ResumeInventoryItem("#douran li:nth-child(2)", ResumeInventoryItem.BulletType, [], "Built services."),
            new ResumeInventoryItem(".key-front-end", ResumeInventoryItem.KeyType, ["key-front-end"], "TypeScript"),
        ]);
    }

    private static AiVerdictUpdate Verdict(int score = 80, string? delta = null)
    {
        var update = new AiVerdictUpdate
        {
            AiScore = score,
            AiVerdict = AiVerdict.Match,
            Fingerprint = JobContent.Fingerprint("job text"),
        };
        if (delta != null) update.Delta = JsonDocument.Parse(delta).RootElement;
        return update;
    }

    private static long SeedJob(GoldenDatabase db, string code, JobState state = JobState.AiPending,
        ResumeContext? options = null, ResumeText? text = null)
    {
        db.SaveSearchJob(code, $"https://example.com/jobs/{code}");
        db.ExecuteRaw(
            @"UPDATE Job SET State = $state, Content = $content, Options = $options, ResumeText = $text WHERE Code = $code",
            ("$state", state.ToString()),
            ("$content", "job text"),
            ("$options", (object?)options == null ? DBNull.Value : Newtonsoft.Json.JsonConvert.SerializeObject(options)),
            ("$text", (object?)text == null ? DBNull.Value : Newtonsoft.Json.JsonConvert.SerializeObject(text, ResumeTextJson)),
            ("$code", code));
        return db.JobId(code);
    }

    private const string FullDelta =
        """{"keys": ["JAVA", "SQL"], "notIncluded": ["#douran"], "included": ["#douran li:nth-child(2)"], "length": 2, "texts": {"title": "Senior Java Engineer", "summary": "Backend focus."}}""";

    [Fact]
    public void Valid_delta_applies_selection_and_proposes_texts()
    {
        using var db = new GoldenDatabase();
        var options = new ResumeContext();
        options.Keys["DOTNET"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "asp.net" };
        var id = SeedJob(db, "t1", options: options);

        db.Database.Job.ApplyAiVerdict(id, Verdict(delta: FullDelta), Inventory());

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.Attention, job.State);
        Assert.NotNull(job.AiOptions);
        Assert.False(job.AiOptions!.Keys.DOTNET);
        Assert.True(job.AiOptions.Keys.JAVA);
        Assert.True(job.AiOptions.Keys.SQL);
        Assert.Equal(["#douran"], job.AiOptions.NotIncluded);
        Assert.Equal(["#douran li:nth-child(2)"], job.AiOptions.Included);
        Assert.Equal(2, job.AiOptions.Length);
        Assert.Equal("Senior Java Engineer", job.ResumeText!["title"].Proposal);
        Assert.Equal(ResumeTextSlotStatus.Pending, job.ResumeText["title"].Status);
        Assert.Null(job.ResumeText["title"].Live);
        Assert.Contains("**AI delta**", job.Log);
    }

    [Fact]
    public void Invalid_texts_do_not_drop_a_valid_selection()
    {
        using var db = new GoldenDatabase();
        var id = SeedJob(db, "t2");
        var delta =
            """{"keys": ["JAVA"], "notIncluded": [], "included": [], "length": 1, "texts": {"title": "two\nlines title"}}""";

        db.Database.Job.ApplyAiVerdict(id, Verdict(delta: delta), Inventory());

        var job = db.Database.Job.Fetch(id)!;
        Assert.NotNull(job.AiOptions);
        Assert.True(job.AiOptions!.Keys.JAVA);
        Assert.Null(job.ResumeText);
        Assert.Contains("texts dropped", job.Log);
    }

    [Fact]
    public void Invalid_selection_does_not_drop_valid_texts()
    {
        using var db = new GoldenDatabase();
        var id = SeedJob(db, "t3");
        var delta =
            """{"keys": ["COBOL"], "notIncluded": [], "included": [], "length": 1, "texts": {"summary": "Backend focus."}}""";

        db.Database.Job.ApplyAiVerdict(id, Verdict(delta: delta), Inventory());

        var job = db.Database.Job.Fetch(id)!;
        Assert.Null(job.AiOptions);
        Assert.Equal("Backend focus.", job.ResumeText!["summary"].Proposal);
        Assert.Contains("selection dropped", job.Log);
    }

    [Theory]
    [InlineData("""{"keys": ["DOTNET","JAVA","PYTHON","GOLANG","SQL","FRONT_END","WEB","MACHINE_LEARNING","NETWORK"], "notIncluded": [], "included": [], "length": 1}""", "keys")]
    [InlineData("""{"keys": [], "notIncluded": ["#nope"], "included": [], "length": 1}""", "selector")]
    [InlineData("""{"keys": [], "notIncluded": [], "included": [".key-missing"], "length": 1}""", "selector")]
    [InlineData("""{"keys": [], "notIncluded": [], "included": [], "length": 3}""", "length")]
    [InlineData("""{"keys": [], "notIncluded": [], "included": [], "length": 0}""", "length")]
    public void Invalid_selections_are_dropped(string selection, string hint)
    {
        using var db = new GoldenDatabase();
        var id = SeedJob(db, $"t{hint.Length}");
        var update = Verdict(delta: $"{{{selection.Trim('{', '}')}}}");

        db.Database.Job.ApplyAiVerdict(id, update, Inventory());

        var job = db.Database.Job.Fetch(id)!;
        Assert.Null(job.AiOptions);
        Assert.Contains("selection dropped", job.Log);
    }

    [Fact]
    public void Selector_cap_is_enforced()
    {
        using var db = new GoldenDatabase();
        var id = SeedJob(db, "t-cap");
        var selectors = string.Join(", ", Enumerable.Range(0, 31).Select(_ => "\"#douran\""));
        var delta = $$"""{"keys": [], "notIncluded": [{{selectors}}], "included": [], "length": 1}""";

        db.Database.Job.ApplyAiVerdict(id, Verdict(delta: delta), Inventory());

        var job = db.Database.Job.Fetch(id)!;
        Assert.Null(job.AiOptions);
        Assert.Contains("selection dropped", job.Log);
    }

    [Fact]
    public void Html_in_texts_is_rejected()
    {
        using var db = new GoldenDatabase();
        var id = SeedJob(db, "t-html");
        var delta =
            """{"keys": ["JAVA"], "notIncluded": [], "included": [], "length": 1, "texts": {"summary": "<b>bold</b>"}}""";

        db.Database.Job.ApplyAiVerdict(id, Verdict(delta: delta), Inventory());

        var job = db.Database.Job.Fetch(id)!;
        Assert.Null(job.ResumeText);
    }

    [Fact]
    public void Non_promoting_verdict_drops_delta_but_keeps_previous_tailoring()
    {
        using var db = new GoldenDatabase();
        var id = SeedJob(db, "t4");

        db.Database.Job.ApplyAiVerdict(id, Verdict(score: 40, delta: FullDelta), Inventory());

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.NotApprovedAI, job.State);
        Assert.Null(job.AiOptions);
        Assert.Null(job.ResumeText);
        Assert.Contains("does not promote", job.Log);

        db.ExecuteRaw($"UPDATE Job SET State = 'AiPending' WHERE JobID = {id}");
        db.ExecuteRaw(
            "UPDATE Job SET AiOptions = '{\"Length\": 2, \"HumanEdited\": false}', " +
            "ResumeText = '{\"title\": {\"live\": null, \"proposal\": \"old\", \"status\": \"pending\"}}' " +
            $"WHERE JobID = {id}");

        db.Database.Job.ApplyAiVerdict(id, Verdict(score: 40, delta: FullDelta), Inventory());

        var kept = db.Database.Job.Fetch(id)!;
        Assert.NotNull(kept.AiOptions);
        Assert.Equal("old", kept.ResumeText!["title"].Proposal);
    }

    [Fact]
    public void Fingerprint_mismatch_drops_delta()
    {
        using var db = new GoldenDatabase();
        var id = SeedJob(db, "t5");
        var update = Verdict(delta: FullDelta);
        update.Fingerprint = "deadbeef";

        db.Database.Job.ApplyAiVerdict(id, update, Inventory());

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.AiPending, job.State);
        Assert.Null(job.AiOptions);
        Assert.Null(job.ResumeText);
    }

    [Fact]
    public void Rejected_proposal_is_not_restored_by_a_same_proposal_rerun()
    {
        var current = new ResumeText
        {
            ["title"] = new ResumeTextSlot { Proposal = "same wording", Status = ResumeTextSlotStatus.Rejected },
        };
        var merged = AiTailoring.Merge(current, new Dictionary<string, string> { ["title"] = "same wording" });

        Assert.Equal(ResumeTextSlotStatus.Rejected, merged["title"].Status);
        Assert.Equal("same wording", merged["title"].Proposal);

        merged = AiTailoring.Merge(merged, new Dictionary<string, string> { ["title"] = "new wording" });
        Assert.Equal(ResumeTextSlotStatus.Pending, merged["title"].Status);
        Assert.Equal("new wording", merged["title"].Proposal);
    }

    [Fact]
    public void Merge_preserves_live_text()
    {
        var current = new ResumeText
        {
            ["title"] = new ResumeTextSlot { Live = "Human title", Status = ResumeTextSlotStatus.Accepted },
        };

        var merged = AiTailoring.Merge(current, new Dictionary<string, string> { ["title"] = "AI proposal" });

        Assert.Equal("Human title", merged["title"].Live);
        Assert.Equal("AI proposal", merged["title"].Proposal);
        Assert.Equal(ResumeTextSlotStatus.Pending, merged["title"].Status);
    }

    [Fact]
    public void Accept_reject_and_live_text_operations()
    {
        using var db = new GoldenDatabase();
        var id = SeedJob(db, "t6", text: new ResumeText
        {
            ["title"] = new ResumeTextSlot { Proposal = "AI title", Status = ResumeTextSlotStatus.Pending },
        });

        Assert.False(db.Database.Job.AcceptTextSlot(id, "summary"));
        Assert.True(db.Database.Job.AcceptTextSlot(id, "title"));

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal("AI title", job.ResumeText!["title"].Live);
        Assert.Null(job.ResumeText["title"].Proposal);
        Assert.Equal(ResumeTextSlotStatus.Accepted, job.ResumeText["title"].Status);

        Assert.True(db.Database.Job.WriteLiveText(id, "summary", "  Backend engineer. "));
        Assert.True(db.Database.Job.RejectTextSlot(id, "summary"));

        job = db.Database.Job.Fetch(id)!;
        Assert.Equal("Backend engineer.", job.ResumeText!["summary"].Live);
        Assert.Equal(ResumeTextSlotStatus.Rejected, job.ResumeText["summary"].Status);

        Assert.False(db.Database.Job.WriteLiveText(id, "#unknown li:nth-child(9)", "nope"));
    }

    [Fact]
    public void Accept_ai_selection_copies_into_options_with_human_edited()
    {
        using var db = new GoldenDatabase();
        var id = SeedJob(db, "t7");

        db.Database.Job.ApplyAiVerdict(id, Verdict(delta: FullDelta), Inventory());
        Assert.True(db.Database.Job.AcceptAiOptions(id));

        var job = db.Database.Job.Fetch(id)!;
        Assert.True(job.Options!.HumanEdited);
        Assert.True(job.Options.Keys.JAVA);
        Assert.NotNull(job.AiOptions);
    }

    [Fact]
    public void Human_options_submit_marks_human_edited()
    {
        using var db = new GoldenDatabase();
        var id = SeedJob(db, "t8");

        var controller = new JobController(null!, db.Database, null!);
        controller.Options(id, "{}");

        Assert.True(db.Database.Job.Fetch(id)!.Options!.HumanEdited);
    }

    [Fact]
    public void Resume_selection_precedence_and_live_text()
    {
        var regex = new ResumeContext { JobTitle = "Regex title" };
        var ai = new ResumeContext { JobTitle = "AI title" };
        var human = new ResumeContext { JobTitle = "Human title", HumanEdited = true };

        Assert.Equal("AI title", ResumeHtml.Selection(new Job { Options = regex, AiOptions = ai }).JobTitle);
        Assert.Equal("Human title", ResumeHtml.Selection(new Job { Options = human, AiOptions = ai }).JobTitle);
        Assert.Equal("Regex title", ResumeHtml.Selection(new Job { Options = regex }).JobTitle);

        var job = new Job
        {
            Options = regex,
            ResumeText = new ResumeText
            {
                ["title"] = new ResumeTextSlot { Live = "Live title", Status = ResumeTextSlotStatus.Accepted },
                ["summary"] = new ResumeTextSlot { Proposal = "pending summary", Status = ResumeTextSlotStatus.Pending },
            },
        };
        var live = ResumeHtml.LiveText(job);
        Assert.Equal("Live title", live["title"]);
        Assert.False(live.ContainsKey("summary"));
    }

    [Fact]
    public void Next_payload_carries_options_and_inventory()
    {
        var job = new Job { JobID = 7, Content = "hello" };
        job.Options = new ResumeContext { JobTitle = "Regex title" };

        var payload = AiNextPayload.From(job, "v1");
        Assert.Contains("Regex title", payload.Options);

        var context = AiContextPayload.From([], [], [], "resume text", Inventory(), 60);
        Assert.Equal(5, context.Inventory!.Count);
        Assert.Contains(context.Inventory, item => item.Id == "#douran li:nth-child(2)");
        Assert.NotNull(context.ContextVersion);
    }

    [Fact]
    public void Inventory_parser_builds_slots_blocks_bullets_and_keys()
    {
        var html = """
        <html><body>
        <header class="header"><h1 class="header-job-title">Senior Full Stack Software Developer</h1></header>
        <main><section class="ltr"><h2>Summary</h2><p id="summary">Experienced engineer.</p></section></main>
        <article class="ltr key-java" id="douran"><h3>Douran</h3>
        <ul><li>First bullet.</li><li>Second bullet.</li></ul></article>
        <span class="skill key-front-end">TypeScript</span>
        </body></html>
        """;

        var inventory = ResumeInventoryParser.Parse(html);

        Assert.Contains(inventory.Items, item => item.Id == "title" && item.Type == ResumeInventoryItem.SlotType);
        Assert.Contains(inventory.Items, item => item.Id == "summary" && item.Type == ResumeInventoryItem.SlotType);
        Assert.Contains(inventory.Items, item => item.Id == "#douran" && item.Keys.Contains("key-java"));
        Assert.Contains(inventory.Items, item =>
            item.Id == "#douran li:nth-child(2)" && item.Text == "Second bullet.");
        Assert.Contains(inventory.Items, item => item.Id == ".key-front-end" && item.Type == ResumeInventoryItem.KeyType);
        Assert.True(inventory.TextSlots.Contains("#douran li:nth-child(2)"));
        Assert.True(inventory.SelectionSelectors.Contains("#douran"));
        Assert.False(inventory.TextSlots.Contains("#douran"));
    }
}
