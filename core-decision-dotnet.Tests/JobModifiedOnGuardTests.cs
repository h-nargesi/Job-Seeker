namespace Photon.JobSeeker.Tests;

public class JobModifiedOnGuardTests
{
    private const string AppliedCode = "guard-applied";

    private const string RejectedCode = "guard-rejected";

    private static string ModifiedOn(GoldenDatabase db, string code)
    {
        return Assert.IsType<DateTime>(db.Scalar($"SELECT ModifiedOn FROM Job WHERE Code = '{code}'"))
            .ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static (string Applied, string Rejected) SeedTerminalPair(GoldenDatabase db)
    {
        db.SaveSearchJob(AppliedCode, $"https://example.com/jobs/{AppliedCode}");
        db.SaveSearchJob(RejectedCode, $"https://example.com/jobs/{RejectedCode}");
        db.ExecuteRaw($"UPDATE Job SET State = '{nameof(JobState.Applied)}' WHERE Code = '{AppliedCode}'");
        db.ExecuteRaw($"UPDATE Job SET State = '{nameof(JobState.Rejected)}' WHERE Code = '{RejectedCode}'");
        return (ModifiedOn(db, AppliedCode), ModifiedOn(db, RejectedCode));
    }

    private static void AssertTerminalModifiedOnUnchanged(GoldenDatabase db, (string Applied, string Rejected) before)
    {
        Assert.Equal(before.Applied, ModifiedOn(db, AppliedCode));
        Assert.Equal(before.Rejected, ModifiedOn(db, RejectedCode));
    }

    private static AiVerdictUpdate Verdict(int score = 42, string? fingerprint = null)
    {
        return new AiVerdictUpdate
        {
            AiScore = score,
            AiVerdict = AiVerdict.Possible,
            AiReason = "late verdict",
            Fingerprint = fingerprint ?? "deadbeef",
        };
    }

    [Fact]
    public void UpdateJobContent_keeps_modifiedon_on_terminal_rows()
    {
        using var db = new GoldenDatabase();
        var before = SeedTerminalPair(db);

        Thread.Sleep(1100);
        foreach (var code in new[] { AppliedCode, RejectedCode })
        {
            var job = db.Database.Job.Fetch(db.JobId(code))!;
            job.Html = "<html>updated</html>";
            job.Content = "updated content";
            db.Database.Job.UpdateJobContent(job);
        }

        Assert.Equal("updated content", db.Scalar($"SELECT Content FROM Job WHERE Code = '{AppliedCode}'"));
        AssertTerminalModifiedOnUnchanged(db, before);
    }

    [Fact]
    public void RegisterAttempt_keeps_modifiedon_on_terminal_rows()
    {
        using var db = new GoldenDatabase();
        var before = SeedTerminalPair(db);

        Thread.Sleep(1100);
        db.Database.Job.RegisterAttempt(db.JobId(AppliedCode), 9, "9: guard");
        db.Database.Job.RegisterAttempt(db.JobId(RejectedCode), 9, "9: guard");

        Assert.Equal(9L, db.Scalar($"SELECT Attempts FROM Job WHERE Code = '{AppliedCode}'"));
        AssertTerminalModifiedOnUnchanged(db, before);
    }

    [Fact]
    public void ChangeOptions_keeps_modifiedon_on_terminal_rows()
    {
        using var db = new GoldenDatabase();
        var before = SeedTerminalPair(db);

        Thread.Sleep(1100);
        db.Database.Job.ChangeOptions(db.JobId(AppliedCode), new ResumeContext { JobTitle = "Guarded" });
        db.Database.Job.ChangeOptions(db.JobId(RejectedCode), new ResumeContext { JobTitle = "Guarded" });

        Assert.NotNull(db.Scalar($"SELECT Options FROM Job WHERE Code = '{AppliedCode}'"));
        AssertTerminalModifiedOnUnchanged(db, before);
    }

    [Fact]
    public void WriteLiveText_keeps_modifiedon_on_terminal_rows()
    {
        using var db = new GoldenDatabase();
        var before = SeedTerminalPair(db);

        Thread.Sleep(1100);
        Assert.True(db.Database.Job.WriteLiveText(db.JobId(AppliedCode), ResumeInventory.TitleSlot, "Guarded title"));
        Assert.True(db.Database.Job.WriteLiveText(db.JobId(RejectedCode), ResumeInventory.TitleSlot, "Guarded title"));

        Assert.IsType<string>(db.Scalar($"SELECT ResumeText FROM Job WHERE Code = '{AppliedCode}'"));
        AssertTerminalModifiedOnUnchanged(db, before);
    }

    [Fact]
    public void RemoveHtmlContent_keeps_modifiedon_on_terminal_rows()
    {
        using var db = new GoldenDatabase();
        var before = SeedTerminalPair(db);
        db.ExecuteRaw($"UPDATE Job SET Html = '<html>x</html>', Content = 'text' WHERE Code IN ('{AppliedCode}', '{RejectedCode}')");

        Thread.Sleep(1100);
        db.Database.Job.RemoveHtmlContent(db.JobId(AppliedCode));
        db.Database.Job.RemoveHtmlContent(db.JobId(RejectedCode));

        Assert.Equal(DBNull.Value, db.Scalar($"SELECT Html FROM Job WHERE Code = '{AppliedCode}'"));
        AssertTerminalModifiedOnUnchanged(db, before);
    }

    [Fact]
    public void UpdateScrapedJob_keeps_modifiedon_on_terminal_rows()
    {
        using var db = new GoldenDatabase();
        var before = SeedTerminalPair(db);

        Thread.Sleep(1100);
        foreach (var code in new[] { AppliedCode, RejectedCode })
        {
            var job = db.Database.Job.Fetch(db.JobId(code))!;
            job.Title = "Scraped update";
            db.Database.Job.UpdateScrapedJob(job, codeChanged: false, linkFound: false, includeState: false);
        }

        Assert.Equal("Scraped update", db.Scalar($"SELECT Title FROM Job WHERE Code = '{RejectedCode}'"));
        Assert.Equal(nameof(JobState.Applied), db.Scalar($"SELECT State FROM Job WHERE Code = '{AppliedCode}'"));
        AssertTerminalModifiedOnUnchanged(db, before);
    }

    [Fact]
    public void ApplyAiVerdict_informational_keeps_modifiedon_on_terminal_rows()
    {
        using var db = new GoldenDatabase();
        var before = SeedTerminalPair(db);
        db.ExecuteRaw($"UPDATE Job SET Content = 'job text' WHERE Code IN ('{AppliedCode}', '{RejectedCode}')");

        Thread.Sleep(1100);
        Assert.True(db.Database.Job.ApplyAiVerdict(db.JobId(AppliedCode), Verdict(fingerprint: JobContent.Fingerprint("job text"))));
        Assert.True(db.Database.Job.ApplyAiVerdict(db.JobId(RejectedCode), Verdict(fingerprint: JobContent.Fingerprint("job text"))));

        Assert.Equal(42L, db.Scalar($"SELECT AiScore FROM Job WHERE Code = '{AppliedCode}'"));
        Assert.Equal(nameof(JobState.Rejected), db.Scalar($"SELECT State FROM Job WHERE Code = '{RejectedCode}'"));
        Assert.Contains("informational", Assert.IsType<string>(db.Scalar($"SELECT Log FROM Job WHERE Code = '{AppliedCode}'")));
        AssertTerminalModifiedOnUnchanged(db, before);
    }

    [Fact]
    public void Guarded_writes_still_bump_modifiedon_on_non_terminal_rows()
    {
        using var db = new GoldenDatabase();
        var codes = new[] { "bump-content", "bump-attempt", "bump-options", "bump-text", "bump-scrape", "bump-verdict" };
        foreach (var code in codes) db.SaveSearchJob(code, $"https://example.com/jobs/{code}");
        db.ExecuteRaw("UPDATE Job SET State = 'Attention' WHERE Code = 'bump-content'");
        var before = codes.Select(code => ModifiedOn(db, code)).ToArray();

        Thread.Sleep(1100);

        var content = db.Database.Job.Fetch(db.JobId("bump-content"))!;
        content.Html = "<html>h</html>";
        content.Content = "c";
        db.Database.Job.UpdateJobContent(content);
        db.Database.Job.RegisterAttempt(db.JobId("bump-attempt"), 2, "2: bump");
        db.Database.Job.ChangeOptions(db.JobId("bump-options"), new ResumeContext { JobTitle = "Bump" });
        db.Database.Job.WriteLiveText(db.JobId("bump-text"), ResumeInventory.TitleSlot, "Bumped");
        var scraped = db.Database.Job.Fetch(db.JobId("bump-scrape"))!;
        scraped.Title = "Bumped";
        db.Database.Job.UpdateScrapedJob(scraped, codeChanged: false, linkFound: false, includeState: false);
        db.Database.Job.ApplyAiVerdict(db.JobId("bump-verdict"), Verdict(10));

        for (var i = 0; i < codes.Length; i++)
            Assert.NotEqual(before[i], ModifiedOn(db, codes[i]));
    }

    [Fact]
    public void ChangeStateManually_still_bumps_modifiedon()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("bump-manual", "https://example.com/jobs/bump-manual");
        db.ExecuteRaw("UPDATE Job SET State = 'Attention' WHERE Code = 'bump-manual'");
        var before = ModifiedOn(db, "bump-manual");

        Thread.Sleep(1100);
        Assert.True(db.Database.Job.ChangeStateManually(db.JobId("bump-manual"), JobState.Rejected));

        Assert.Equal(nameof(JobState.Rejected), db.Scalar("SELECT State FROM Job"));
        Assert.NotEqual(before, ModifiedOn(db, "bump-manual"));
    }

    [Fact]
    public void MarkApplied_still_bumps_modifiedon()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("bump-applied", "https://example.com/jobs/bump-applied");
        var before = ModifiedOn(db, "bump-applied");

        Thread.Sleep(1100);
        Assert.True(db.Database.Job.MarkApplied(db.JobId("bump-applied"), "guard test"));

        Assert.Equal(nameof(JobState.Applied), db.Scalar("SELECT State FROM Job"));
        Assert.NotEqual(before, ModifiedOn(db, "bump-applied"));
    }

    [Fact]
    public void InsertFromSearch_stores_local_time_timestamps()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("local-search", "https://example.com/jobs/local-search");

        AssertWithinNow(Timestamp(db, "RegTime"));
        AssertWithinNow(Timestamp(db, "ModifiedOn"));
    }

    [Fact]
    public void InsertJob_stores_local_time_timestamps()
    {
        using var db = new GoldenDatabase();
        db.Database.Job.InsertJob(new Job
        {
            AgencyID = GoldenDatabase.AgencyId,
            Country = "NL",
            Code = "local-insert",
            Title = "Local Time",
            State = JobState.Attention,
            Url = "https://example.com/jobs/local-insert",
        });

        AssertWithinNow(Timestamp(db, "RegTime"));
        AssertWithinNow(Timestamp(db, "ModifiedOn"));
    }

    private static DateTime Timestamp(GoldenDatabase db, string column)
    {
        return Assert.IsType<DateTime>(db.Scalar($"SELECT {column} FROM Job"));
    }

    private static void AssertWithinNow(DateTime stored)
    {
        Assert.True(Math.Abs((stored - DateTime.Now).TotalSeconds) <= 90,
            $"{stored:O} is not within 90s of local now {DateTime.Now:O}");
    }
}
