using Microsoft.AspNetCore.Mvc;

namespace Photon.JobSeeker.Tests;

public class MonitorPanelTests
{
    [Fact]
    public void Calibration_counts_bands_unknowns_and_inversions()
    {
        using var db = new GoldenDatabase();
        Seed(db, "m1", score: 90, aiScore: 90, aiVerdict: "StrongMatch",
            seniority: "Senior", skills: "[\"C#\"]", salaryMin: 50, salaryMax: 60);
        Seed(db, "m2", score: 90, aiScore: 90, aiVerdict: "Match",
            seniority: "Unknown", skills: "[]");
        Seed(db, "m3", score: 90, aiScore: 30, aiVerdict: "NoMatch",
            seniority: "Unknown", salaryMin: 90, salaryMax: 40);
        Seed(db, "m4", score: 90, aiScore: null, aiVerdict: null);
        Seed(db, "m5", score: 90, aiScore: 10, aiVerdict: "Error");

        var calibration = db.Database.Job.Calibration();

        Assert.Equal(3, calibration.Judged);
        Assert.Equal(1, calibration.BandMismatches);
        Assert.Equal(1, calibration.InvertedSalaries);
        Assert.Equal(2, calibration.KeywordRichWithoutSkills);

        var seniority = Assert.Single(calibration.Fields, f => f.Field == "Seniority");
        Assert.Equal(2, seniority.Missing);

        var salary = Assert.Single(calibration.Fields, f => f.Field == "Salary");
        Assert.Equal(1, salary.Missing);

        var skills = Assert.Single(calibration.Fields, f => f.Field == "Skills");
        Assert.Equal(2, skills.Missing);
    }

    [Fact]
    public void Queue_health_counts_pending_and_error_with_oldest_age()
    {
        using var db = new GoldenDatabase();
        Seed(db, "q1", score: 90, state: "AiPending", regTime: "2026-09-01 10:00:00");
        Seed(db, "q2", score: 90, state: "AiPending", regTime: "2026-09-10 10:00:00");
        Seed(db, "q3", score: 90, state: "AIError");

        var queue = db.Database.Job.QueueHealth();

        Assert.Equal(2, queue.AiPending);
        Assert.Equal(1, queue.AiError);
        Assert.NotNull(queue.OldestPending);
        Assert.Equal(2026, queue.OldestPending!.Value.Year);
    }

    [Fact]
    public void Controller_serves_the_panel_with_runs_and_health()
    {
        using var db = new GoldenDatabase();
        db.Database.AiRun.Upsert(new AiRun
        {
            RunID = "20260923-101010",
            StartedUtc = "2026-09-23T10:10:10Z",
            FinishedUtc = "2026-09-23T10:12:10Z",
            ExitCode = 0,
            Jobs = 3,
            Promoted = 1,
            Retries = 1,
            CallMs = 40000,
            MaxCallTokens = 4100,
            MaxCallMs = 15000,
            ErrorJobIds = "[7]",
        });

        var result = new MonitorController(db.Database).Index(null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MonitorViewModel>(view.Model);
        Assert.Single(model.Runs);
        var run = model.Runs[0];
        Assert.Equal([7], run.ErrorJobIdList);
        Assert.Equal(5, run.Calls);
        Assert.Equal(8.0, run.AvgCallSeconds);
        Assert.Equal(4100, run.MaxCallTokens);
        Assert.Equal(15000, run.MaxCallMs);
        Assert.Equal(0, model.Queue.AiPending);
    }

    [Fact]
    public void Controller_group_defaults_to_agency_and_only_accepts_country()
    {
        using var db = new GoldenDatabase();
        var controller = new MonitorController(db.Database);

        var model = Assert.IsType<MonitorViewModel>(Assert.IsType<ViewResult>(controller.Index(null)).Model);
        Assert.Equal("agency", model.Group);

        model = Assert.IsType<MonitorViewModel>(Assert.IsType<ViewResult>(controller.Index("bogus")).Model);
        Assert.Equal("agency", model.Group);

        model = Assert.IsType<MonitorViewModel>(Assert.IsType<ViewResult>(controller.Index("country")).Model);
        Assert.Equal("country", model.Group);
    }

    [Fact]
    public void Stages_group_by_agency_counts_labels_and_rates()
    {
        using var db = new GoldenDatabase();
        db.ExecuteRaw(
            "INSERT INTO Agency (AgencyID, Title, Active, Domain, Link) VALUES (2, 'Second', 3, 'two.example.com', 'https://two.example.com')");
        Seed(db, "s-att", 90, state: "Attention");
        Seed(db, "s-reg", 90, state: "NotApprovedRegex");
        Seed(db, "s-pen", 90, state: "AiPending");
        Seed(db, "s-app", 90, state: "Applied");
        db.ExecuteRaw("UPDATE Job SET AgencyID = 2 WHERE Code = 's-app'");

        var rows = db.Database.Job.Stages(JobBusiness.GroupAgency);

        Assert.Equal(["Golden", "Second"], rows.Select(row => row.Label).ToList());
        var golden = rows[0];
        Assert.Equal(1, golden.Attention);
        Assert.Equal(1, golden.NotApprovedRegex);
        Assert.Equal(1, golden.AiPending);
        Assert.Equal("66.7%", golden.RegexPassRate);
        Assert.Equal("100%", golden.AiPromoteRate);
        Assert.Equal("0%", golden.DispositionRate);
        var second = rows[1];
        Assert.Equal(1, second.Applied);
        Assert.Equal("100%", second.RegexPassRate);
        Assert.Equal("0%", second.AiPromoteRate);
        Assert.Equal("n/a", second.DispositionRate);
    }

    [Fact]
    public void Stages_group_by_country_with_none_label()
    {
        using var db = new GoldenDatabase();
        Seed(db, "n-nl-att", 90, state: "Attention");
        Seed(db, "n-nl-reg", 90, state: "NotApprovedRegex");
        Seed(db, "n-de-app", 90, state: "Applied");
        Seed(db, "n-none-att", 90, state: "Attention");
        db.ExecuteRaw("UPDATE Job SET Country = 'DE' WHERE Code = 'n-de-app'");
        db.ExecuteRaw("UPDATE Job SET Country = '' WHERE Code = 'n-none-att'");

        var rows = db.Database.Job.Stages(JobBusiness.GroupCountry);

        Assert.Equal(["(none)", "DE", "NL"], rows.Select(row => row.Label).ToList());
        var nl = rows.Single(row => row.Label == "NL");
        Assert.Equal(1, nl.Attention);
        Assert.Equal("50%", nl.RegexPassRate);
        Assert.Equal("100%", nl.AiPromoteRate);
        Assert.Equal("0%", nl.DispositionRate);
        var de = rows.Single(row => row.Label == "DE");
        Assert.Equal(1, de.Applied);
        Assert.Equal("100%", de.RegexPassRate);
        Assert.Equal("0%", de.AiPromoteRate);
        Assert.Equal("n/a", de.DispositionRate);
        var none = rows.Single(row => row.Label == "(none)");
        Assert.Equal(1, none.Attention);
        Assert.Equal("100%", none.AiPromoteRate);
    }

    [Fact]
    public void Verdict_distribution_counts_every_verdict()
    {
        using var db = new GoldenDatabase();
        Seed(db, "v1", 90, state: "NotApprovedAI", aiVerdict: "StrongMatch");
        Seed(db, "v2", 90, state: "NotApprovedAI", aiVerdict: "Match");
        Seed(db, "v3", 90, state: "NotApprovedAI", aiVerdict: "NoMatch");
        Seed(db, "v4", 90, state: "AIError", aiVerdict: "Error");
        Seed(db, "v5", 90, state: "Attention");

        var verdicts = db.Database.Job.VerdictDistribution();

        Assert.Equal(1, verdicts.StrongMatch);
        Assert.Equal(1, verdicts.Match);
        Assert.Equal(1, verdicts.NoMatch);
        Assert.Equal(1, verdicts.Error);
        Assert.Equal(3, verdicts.Judged);
        Assert.Equal(2, verdicts.AtLeastMatch);
    }

    [Fact]
    public void Pending_ages_bucket_by_regtime_for_pending_only()
    {
        using var db = new GoldenDatabase();
        var now = DateTime.Now;
        Seed(db, "p1", 90, state: "AiPending", regTime: now.AddHours(-2).ToString("yyyy-MM-dd HH:mm:ss"));
        Seed(db, "p2", 90, state: "AiPending", regTime: now.AddHours(-12).ToString("yyyy-MM-dd HH:mm:ss"));
        Seed(db, "p3", 90, state: "AiPending", regTime: now.AddHours(-50).ToString("yyyy-MM-dd HH:mm:ss"));
        Seed(db, "p4", 90, state: "AiPending", regTime: now.AddDays(-5).ToString("yyyy-MM-dd HH:mm:ss"));
        Seed(db, "p5", 90, state: "Attention", regTime: now.AddDays(-5).ToString("yyyy-MM-dd HH:mm:ss"));

        var ages = db.Database.Job.PendingAges();

        Assert.Equal(1, ages.To6Hours);
        Assert.Equal(1, ages.From6To24Hours);
        Assert.Equal(1, ages.From1To3Days);
        Assert.Equal(1, ages.Over3Days);
        Assert.Equal(4, ages.Total);
    }

    private static void Seed(GoldenDatabase db, string code, int? score, string state = "Attention",
        int? aiScore = null, string? aiVerdict = null, string? seniority = null,
        string? skills = null, int? salaryMin = null, int? salaryMax = null, string? regTime = null)
    {
        db.SaveSearchJob(code, $"https://example.com/jobs/{code}");
        db.ExecuteRaw(
            @"UPDATE Job SET
                State = $state, Score = $score, RegTime = COALESCE($regTime, RegTime),
                AiScore = $aiScore, AiVerdict = $aiVerdict, AiSeniority = $seniority,
                AiSkills = $skills, AiSalaryMin = $salaryMin, AiSalaryMax = $salaryMax
              WHERE Code = $code",
            ("$state", state),
            ("$score", (object?)score ?? DBNull.Value),
            ("$regTime", (object?)regTime ?? DBNull.Value),
            ("$aiScore", (object?)aiScore ?? DBNull.Value),
            ("$aiVerdict", (object?)aiVerdict ?? DBNull.Value),
            ("$seniority", (object?)seniority ?? DBNull.Value),
            ("$skills", (object?)skills ?? DBNull.Value),
            ("$salaryMin", (object?)salaryMin ?? DBNull.Value),
            ("$salaryMax", (object?)salaryMax ?? DBNull.Value),
            ("$code", code));
    }
}
