namespace Photon.JobSeeker.Tests;

public class CleanProgressTests
{
    private static long Seed(
        GoldenDatabase db,
        string code,
        JobState state,
        long? score = 100,
        string? content = "job text",
        int? aiScore = null,
        string? html = "<p>html</p>")
    {
        db.SaveSearchJob(code, $"https://example.com/jobs/{code}");
        db.ExecuteRaw(
            @"UPDATE Job SET State = $state, Score = $score, Content = $content, Html = $html,
AiScore = $ai WHERE Code = $code",
            ("$state", state.ToString()),
            ("$score", (object?)score ?? DBNull.Value),
            ("$content", (object?)content ?? DBNull.Value),
            ("$html", (object?)html ?? DBNull.Value),
            ("$ai", (object?)aiScore ?? DBNull.Value),
            ("$code", code));
        return db.JobId(code);
    }

    [Fact]
    public void Clean_reports_stage_progress_to_100()
    {
        using var db = new GoldenDatabase();
        Seed(db, "cp1", JobState.Attention);
        var reports = new List<(int Percent, string Stage)>();

        db.Database.Job.Clean(1, (percent, stage) => reports.Add((percent, stage)));

        Assert.Equal(
        [
            (0, "deleting old jobs"),
            (17, "trimming attention html"),
            (33, "trimming rejected html"),
            (50, "purging below-floor ai content"),
            (67, "purging below-floor regex content"),
            (83, "vacuuming"),
            (100, "done"),
        ], reports);
    }

    [Fact]
    public void Clean_without_reporter_still_cleans()
    {
        using var db = new GoldenDatabase();
        Seed(db, "cp2", JobState.NotApprovedAI);

        db.Database.Job.Clean(1);

        Assert.Equal(DBNull.Value, db.Scalar("SELECT Html FROM Job WHERE Code = 'cp2'"));
        Assert.Equal("job text", db.Scalar("SELECT Content FROM Job WHERE Code = 'cp2'"));
    }

    [Fact]
    public void TryBeginClean_guards_concurrent_starts()
    {
        Assert.True(JobBusiness.TryBeginClean(out var first));
        Assert.Same(first, JobBusiness.CurrentCleanProcess);

        Assert.False(JobBusiness.TryBeginClean(out var running));
        Assert.Same(first, running);

        first.Running = false;
        Assert.True(JobBusiness.TryBeginClean(out var next));
        Assert.NotSame(first, next);
        next.Running = false;
    }
}
