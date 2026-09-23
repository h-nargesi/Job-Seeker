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
            ErrorJobIds = "[7]",
        });

        var result = new MonitorController(db.Database).Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MonitorViewModel>(view.Model);
        Assert.Single(model.Runs);
        Assert.Equal([7], model.Runs[0].ErrorJobIdList);
        Assert.Equal(0, model.Queue.AiPending);
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
