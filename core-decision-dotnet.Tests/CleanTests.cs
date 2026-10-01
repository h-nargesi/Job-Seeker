namespace Photon.JobSeeker.Tests;

public class CleanTests
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
    public void Clean_purges_html_only_and_leaves_error_alone()
    {
        using var db = new GoldenDatabase();
        Seed(db, "cl1", JobState.NotApprovedRegex);
        Seed(db, "cl2", JobState.NotApprovedAI);
        Seed(db, "cl3", JobState.AIError);
        Seed(db, "cl4", JobState.Attention);

        db.Database.Job.Clean(1);

        Assert.Equal(DBNull.Value, db.Scalar("SELECT Html FROM Job WHERE Code = 'cl1'"));
        Assert.Equal("job text", db.Scalar("SELECT Content FROM Job WHERE Code = 'cl1'"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Html FROM Job WHERE Code = 'cl2'"));
        Assert.Equal("job text", db.Scalar("SELECT Content FROM Job WHERE Code = 'cl2'"));
        Assert.Equal("<p>html</p>", db.Scalar("SELECT Html FROM Job WHERE Code = 'cl3'"));
        Assert.Equal("job text", db.Scalar("SELECT Content FROM Job WHERE Code = 'cl3'"));
        Assert.Equal("<p>html</p>", db.Scalar("SELECT Html FROM Job WHERE Code = 'cl4'"));
        Assert.Equal("job text", db.Scalar("SELECT Content FROM Job WHERE Code = 'cl4'"));
    }

    [Fact]
    public void Clean_attention_html_evicts_outside_top_100()
    {
        using var db = new GoldenDatabase();
        for (var i = 0; i < 102; i++)
            Seed(db, $"att-{i}", JobState.Attention);

        db.Database.Job.Clean(1);

        Assert.Equal(100L, db.Scalar(
            "SELECT COUNT(*) FROM Job WHERE State = 'Attention' AND Html IS NOT NULL"));
    }

    [Fact]
    public void Clean_sweeps_below_floor_content()
    {
        using var db = new GoldenDatabase();
        Seed(db, "sw-nai-low", JobState.NotApprovedAI, aiScore: 20);
        Seed(db, "sw-nai-high", JobState.NotApprovedAI, aiScore: 40);
        Seed(db, "sw-regex-low", JobState.NotApprovedRegex, score: 50);
        Seed(db, "sw-regex-high", JobState.NotApprovedRegex, score: 80);
        Seed(db, "sw-att-low", JobState.Attention, aiScore: 20);

        db.Database.Job.Clean(1);

        Assert.Equal(DBNull.Value, db.Scalar("SELECT Content FROM Job WHERE Code = 'sw-nai-low'"));
        Assert.Equal("job text", db.Scalar("SELECT Content FROM Job WHERE Code = 'sw-nai-high'"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Content FROM Job WHERE Code = 'sw-regex-low'"));
        Assert.Equal("job text", db.Scalar("SELECT Content FROM Job WHERE Code = 'sw-regex-high'"));
        Assert.Equal("job text", db.Scalar("SELECT Content FROM Job WHERE Code = 'sw-att-low'"));
    }

    [Fact]
    public void Clean_sweep_respects_disabled_purge_floor()
    {
        using var db = new GoldenDatabase();
        db.Database.AppSetting.Save("aipurgefloor", "0");
        Seed(db, "sw-off", JobState.NotApprovedAI, aiScore: 20);

        db.Database.Job.Clean(1);

        Assert.Equal("job text", db.Scalar("SELECT Content FROM Job WHERE Code = 'sw-off'"));
    }
}
