namespace Photon.JobSeeker.Tests;

public class VerdictPurgeTests
{
    private static AiVerdictUpdate Verdict(
        string fingerprint,
        int score = 80,
        AiVerdict verdict = AiVerdict.Match,
        string? reason = "fit")
    {
        return new AiVerdictUpdate
        {
            AiScore = score,
            AiVerdict = verdict,
            AiReason = reason,
            Fingerprint = fingerprint,
            AiSeniority = AiSeniority.Senior,
            AiSkills = ["C#"],
        };
    }

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
    public void Promote_clears_html_keeps_content()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "vp1", JobState.AiPending);

        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict(JobContent.Fingerprint("job text"), 60)));

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.Attention, job.State);
        Assert.Null(job.Html);
        Assert.Equal("job text", job.Content);
    }

    [Fact]
    public void Reject_above_purge_floor_clears_html_keeps_content()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "vp2", JobState.AiPending);

        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict(JobContent.Fingerprint("job text"), 40)));

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.NotApprovedAI, job.State);
        Assert.Null(job.Html);
        Assert.Equal("job text", job.Content);
    }

    [Fact]
    public void Reject_below_purge_floor_clears_html_and_content()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "vp3", JobState.AiPending);

        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict(JobContent.Fingerprint("job text"), 34)));

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.NotApprovedAI, job.State);
        Assert.Null(job.Html);
        Assert.Null(job.Content);
    }

    [Fact]
    public void Reject_at_exact_purge_floor_keeps_content()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "vp4", JobState.AiPending);

        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict(JobContent.Fingerprint("job text"), 35)));

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.NotApprovedAI, job.State);
        Assert.Null(job.Html);
        Assert.Equal("job text", job.Content);
    }

    [Fact]
    public void Error_verdict_preserves_html_and_content()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "vp5", JobState.AiPending);

        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict(
            JobContent.Fingerprint("job text"), 5, AiVerdict.Error, "boom")));

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.AIError, job.State);
        Assert.Equal("<p>html</p>", job.Html);
        Assert.Equal("job text", job.Content);
    }

    [Fact]
    public void Fingerprint_mismatch_clears_nothing()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "vp6", JobState.AiPending);

        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict("deadbeef", 10)));

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.AiPending, job.State);
        Assert.Equal("<p>html</p>", job.Html);
        Assert.Equal("job text", job.Content);
    }

    [Fact]
    public void Informational_verdict_on_attention_clears_nothing()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "vp7", JobState.Attention, aiScore: 90);

        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict(JobContent.Fingerprint("job text"), 10)));

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.Attention, job.State);
        Assert.Equal("<p>html</p>", job.Html);
        Assert.Equal("job text", job.Content);
    }

    [Fact]
    public void PromoteJob_preserves_html_and_content()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "vp8", JobState.AiPending);

        Assert.True(db.Database.Job.PromoteJob(id));

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.Attention, job.State);
        Assert.Equal("<p>html</p>", job.Html);
        Assert.Equal("job text", job.Content);
    }

    [Fact]
    public void Purge_floor_setting_is_read_live()
    {
        using var db = new GoldenDatabase();
        db.Database.AppSetting.Save("aipurgefloor", "50");
        var id = Seed(db, "vp9", JobState.AiPending);

        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict(JobContent.Fingerprint("job text"), 49)));

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.NotApprovedAI, job.State);
        Assert.Null(job.Html);
        Assert.Null(job.Content);
    }
}
