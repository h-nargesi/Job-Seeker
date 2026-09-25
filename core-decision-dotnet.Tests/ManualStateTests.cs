namespace Photon.JobSeeker.Tests;

public class ManualStateTests
{
    [Fact]
    public void ChangeStateManually_updates_state_log_modifiedon()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("ms1", "https://example.com/jobs/ms1");
        db.ExecuteRaw("UPDATE Job SET Title = 'Senior Dev', State = 'Attention', Html = '<html>h</html>', Content = 'text', Log = 'prev' WHERE Code = 'ms1'");
        var before = ModifiedOn(db);

        Thread.Sleep(1100);
        var done = db.Database.Job.ChangeStateManually(db.JobId("ms1"), JobState.Applied);

        Assert.True(done);
        Assert.Equal("Applied", db.Scalar("SELECT State FROM Job"));
        Assert.Equal("Senior Dev", db.Scalar("SELECT Title FROM Job"));
        var log = Assert.IsType<string>(db.Scalar("SELECT Log FROM Job"));
        Assert.StartsWith("prev", log);
        Assert.Contains("Manual state change Attention→Applied", log);
        Assert.NotEqual(before, ModifiedOn(db));
    }

    [Fact]
    public void ChangeStateManually_keeps_content_on_rejected()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("ms2", "https://example.com/jobs/ms2");
        db.ExecuteRaw("UPDATE Job SET State = 'Attention', Html = '<html>h</html>', Content = 'text' WHERE Code = 'ms2'");

        var done = db.Database.Job.ChangeStateManually(db.JobId("ms2"), JobState.Rejected);

        Assert.True(done);
        Assert.Equal("Rejected", db.Scalar("SELECT State FROM Job"));
        Assert.Equal("<html>h</html>", db.Scalar("SELECT Html FROM Job"));
        Assert.Equal("text", db.Scalar("SELECT Content FROM Job"));
    }

    [Fact]
    public void ChangeStateManually_rejects_revaluation()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("ms3", "https://example.com/jobs/ms3");
        db.ExecuteRaw("UPDATE Job SET State = 'Attention' WHERE Code = 'ms3'");

        var done = db.Database.Job.ChangeStateManually(db.JobId("ms3"), JobState.Revaluation);

        Assert.False(done);
        Assert.Equal("Attention", db.Scalar("SELECT State FROM Job"));
    }

    [Fact]
    public void ChangeStateManually_rejects_same_state()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("ms4", "https://example.com/jobs/ms4");
        db.ExecuteRaw("UPDATE Job SET State = 'Attention', Log = 'prev' WHERE Code = 'ms4'");

        var done = db.Database.Job.ChangeStateManually(db.JobId("ms4"), JobState.Attention);

        Assert.False(done);
        Assert.Equal("Attention", db.Scalar("SELECT State FROM Job"));
        Assert.Equal("prev", db.Scalar("SELECT Log FROM Job"));
    }

    [Fact]
    public void ChangeStateManually_false_for_missing_job()
    {
        using var db = new GoldenDatabase();

        Assert.False(db.Database.Job.ChangeStateManually(999999, JobState.Applied));
    }

    private static string ModifiedOn(GoldenDatabase db)
    {
        return Assert.IsType<DateTime>(db.Scalar("SELECT ModifiedOn FROM Job")).ToString("yyyy-MM-dd HH:mm:ss");
    }
}
