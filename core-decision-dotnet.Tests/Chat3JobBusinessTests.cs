namespace Photon.JobSeeker.Tests;

public class Chat3JobBusinessTests
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
        int attempts = 0,
        string? tries = null,
        string? html = "<p>html</p>")
    {
        db.SaveSearchJob(code, $"https://example.com/jobs/{code}");
        db.ExecuteRaw(
            @"UPDATE Job SET State = $state, Score = $score, Content = $content, Html = $html,
AiScore = $ai, Attempts = $attempts, Tries = $tries WHERE Code = $code",
            ("$state", state.ToString()),
            ("$score", (object?)score ?? DBNull.Value),
            ("$content", (object?)content ?? DBNull.Value),
            ("$html", (object?)html ?? DBNull.Value),
            ("$ai", (object?)aiScore ?? DBNull.Value),
            ("$attempts", attempts),
            ("$tries", (object?)tries ?? DBNull.Value),
            ("$code", code));
        return db.JobId(code);
    }

    [Fact]
    public void ApplyAiVerdict_promotes_from_ai_pending_at_passmark()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "v1", JobState.AiPending);
        var fingerprint = JobContent.Fingerprint("job text");

        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict(fingerprint, 60)));

        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.Attention, job.State);
        Assert.Equal(60, job.AiScore);
        Assert.Equal(AiVerdict.Match, job.AiVerdict);
        Assert.Contains("fit", job.Log);
    }

    [Fact]
    public void ApplyAiVerdict_rejects_below_passmark_from_ai_pending()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "v2", JobState.AiPending);
        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict(JobContent.Fingerprint("job text"), 59)));
        Assert.Equal(JobState.NotApprovedAI, db.Database.Job.Fetch(id)!.State);
    }

    [Fact]
    public void ApplyAiVerdict_error_bypasses_passmark()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "v3", JobState.AiPending);
        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict(
            JobContent.Fingerprint("job text"), 100, AiVerdict.Error, "boom")));
        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.AIError, job.State);
        Assert.Equal(AiVerdict.Error, job.AiVerdict);
    }

    [Fact]
    public void ApplyAiVerdict_does_not_change_state_outside_ai_pending()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "v4", JobState.Attention, aiScore: 90);
        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict(JobContent.Fingerprint("job text"), 10)));
        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.Attention, job.State);
        Assert.Equal(10, job.AiScore);
        Assert.Contains("informational", job.Log);
    }

    [Fact]
    public void ApplyAiVerdict_fingerprint_mismatch_upserts_without_state_change()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "v5", JobState.AiPending);
        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict("deadbeef", 90)));
        var job = db.Database.Job.Fetch(id)!;
        Assert.Equal(JobState.AiPending, job.State);
        Assert.Equal(90, job.AiScore);
        Assert.Contains("fingerprint mismatch", job.Log);
    }

    [Fact]
    public void ApplyAiVerdict_null_content_counts_as_mismatch()
    {
        using var db = new GoldenDatabase();
        var id = Seed(db, "v6", JobState.AiPending, content: null);
        Assert.True(db.Database.Job.ApplyAiVerdict(id, Verdict(JobContent.Fingerprint(null), 90)));
        Assert.Equal(JobState.AiPending, db.Database.Job.Fetch(id)!.State);
    }

    [Fact]
    public void FetchNextAiPending_skips_null_content()
    {
        using var db = new GoldenDatabase();
        Seed(db, "n1", JobState.AiPending, content: null);
        var ok = Seed(db, "n2", JobState.AiPending);
        Seed(db, "n3", JobState.Attention);

        var next = db.Database.Job.FetchNextAiPending();
        Assert.Equal(ok, next!.JobID);
    }

    [Fact]
    public void FetchNextAiPending_is_highest_effective_score_first()
    {
        using var db = new GoldenDatabase();
        var high = Seed(db, "s-high", JobState.AiPending, score: 100);
        Seed(db, "s-low", JobState.AiPending, score: 50);

        var next = db.Database.Job.FetchNextAiPending();
        Assert.Equal(high, next!.JobID);
    }

    [Fact]
    public void FetchNextAiPending_prefers_newer_regtime_on_score_tie()
    {
        using var db = new GoldenDatabase();
        Seed(db, "t-old", JobState.AiPending);
        var newer = Seed(db, "t-new", JobState.AiPending);
        db.ExecuteRaw("UPDATE Job SET RegTime = datetime('now', '-2 days') WHERE Code = 't-old'");
        db.ExecuteRaw("UPDATE Job SET RegTime = datetime('now', '-1 days') WHERE Code = 't-new'");

        var next = db.Database.Job.FetchNextAiPending();
        Assert.Equal(newer, next!.JobID);
    }

    [Fact]
    public void FetchNextAiPending_prefers_newer_published_at_on_score_tie()
    {
        using var db = new GoldenDatabase();
        var newer = Seed(db, "p-new", JobState.AiPending);
        Seed(db, "p-old", JobState.AiPending);
        var reg = new DateTime(2026, 9, 25, 0, 0, 0);
        db.ExecuteRaw("UPDATE Job SET RegTime = $reg, PublishedAt = $pub WHERE Code = 'p-new'",
            ("$reg", reg), ("$pub", reg.AddDays(-1)));
        db.ExecuteRaw("UPDATE Job SET RegTime = $reg, PublishedAt = $pub WHERE Code = 'p-old'",
            ("$reg", reg), ("$pub", reg.AddDays(-1).AddMinutes(-30)));

        var next = db.Database.Job.FetchNextAiPending();
        Assert.Equal(newer, next!.JobID);
    }

    [Fact]
    public void FetchNextAiPending_applies_age_decay()
    {
        using var db = new GoldenDatabase();
        Seed(db, "d-old", JobState.AiPending, score: 90);
        var fresh = Seed(db, "d-new", JobState.AiPending, score: 70);
        db.ExecuteRaw("UPDATE Job SET RegTime = datetime('now', '-20 days') WHERE Code = 'd-old'");

        var next = db.Database.Job.FetchNextAiPending();
        Assert.Equal(fresh, next!.JobID);
    }

    [Fact]
    public void RequeueJob_only_from_ai_error_or_not_approved_ai_with_content()
    {
        using var db = new GoldenDatabase();
        var ok = Seed(db, "r1", JobState.NotApprovedAI);
        var empty = Seed(db, "r2", JobState.AIError, content: null);
        var attention = Seed(db, "r3", JobState.Attention);

        Assert.True(db.Database.Job.RequeueJob(ok));
        Assert.False(db.Database.Job.RequeueJob(empty));
        Assert.False(db.Database.Job.RequeueJob(attention));
        Assert.Equal(JobState.AiPending, db.Database.Job.Fetch(ok)!.State);
        Assert.Equal(JobState.AIError, db.Database.Job.Fetch(empty)!.State);
        Assert.Equal(JobState.Attention, db.Database.Job.Fetch(attention)!.State);
    }

    [Fact]
    public void PromoteJob_only_from_queue_family_and_writes_log()
    {
        using var db = new GoldenDatabase();
        var pending = Seed(db, "p1", JobState.AiPending);
        var rejected = Seed(db, "p2", JobState.Rejected);

        Assert.True(db.Database.Job.PromoteJob(pending));
        Assert.False(db.Database.Job.PromoteJob(rejected));

        var job = db.Database.Job.Fetch(pending)!;
        Assert.Equal(JobState.Attention, job.State);
        Assert.Null(job.AiScore);
        Assert.Contains("Manually promoted (emergency)", job.Log);
        Assert.Equal(JobState.Rejected, db.Database.Job.Fetch(rejected)!.State);
    }

    [Fact]
    public void ResurrectFloorPassing_resets_attempts_on_purged_regex_rejects()
    {
        using var db = new GoldenDatabase();
        var pass = Seed(db, "x1", JobState.NotApprovedRegex, score: 80, content: null, attempts: 4, tries: "4: old");
        var stay = Seed(db, "x2", JobState.NotApprovedRegex, score: 50, content: null, attempts: 3, tries: "3: old");

        Assert.Equal(1, db.Database.Job.ResurrectFloorPassing(70));

        var revived = db.Database.Job.Fetch(pass)!;
        Assert.Equal(JobState.Saved, revived.State);
        Assert.Equal(0, (long)db.Scalar("SELECT Attempts FROM Job WHERE Code = 'x1'")!);
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Tries FROM Job WHERE Code = 'x1'"));

        var kept = db.Database.Job.Fetch(stay)!;
        Assert.Equal(JobState.NotApprovedRegex, kept.State);
        Assert.Equal(3, (long)db.Scalar("SELECT Attempts FROM Job WHERE Code = 'x2'")!);
    }

    [Fact]
    public void ResetRevaluations_returns_saved_and_clears_attempts()
    {
        using var db = new GoldenDatabase();
        Seed(db, "rv1", JobState.Revaluation, attempts: 4, tries: "stuck");
        db.Database.Job.ResetRevaluations();
        Assert.Equal("Saved", db.Scalar("SELECT State FROM Job"));
        Assert.Equal(0L, db.Scalar("SELECT Attempts FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Tries FROM Job"));
    }

    [Fact]
    public void FetchFrom_only_ai_domain_content_bearing_states()
    {
        using var db = new GoldenDatabase();
        Seed(db, "s1", JobState.Saved);
        Seed(db, "s2", JobState.Rejected);
        Seed(db, "s3", JobState.NotApprovedRegex);
        var pending = Seed(db, "s4", JobState.AiPending);

        Assert.Equal(1, db.Database.Job.FetchFromCount(DateTime.Now.AddDays(1)));
        var job = db.Database.Job.FetchFrom(DateTime.Now.AddDays(1));
        Assert.Equal(pending, job!.JobID);
        Assert.Equal("Revaluation", db.Scalar($"SELECT State FROM Job WHERE JobID = {pending}"));
        Assert.Null(db.Database.Job.FetchFrom(DateTime.Now.AddDays(1)));
    }

    [Fact]
    public void Q_INDEX_v2_category_order_caps_and_blend()
    {
        using var db = new GoldenDatabase();
        Seed(db, "c-att-low", JobState.Attention, score: 300, aiScore: 0);
        Seed(db, "c-att-promoted", JobState.Attention, score: 300, aiScore: null);
        Seed(db, "c-pending", JobState.AiPending, score: 90);
        Seed(db, "c-nai", JobState.NotApprovedAI, score: 300, aiScore: 80);
        Seed(db, "c-applied", JobState.Applied, score: 10);
        Seed(db, "c-regex", JobState.NotApprovedRegex, score: 40, content: null);
        Seed(db, "c-err-high-ai", JobState.AIError, score: 50, aiScore: 100);
        Seed(db, "c-err-high-regex", JobState.AIError, score: 200, aiScore: 0);
        Seed(db, "c-saved", JobState.Saved, score: 999);

        for (var i = 0; i < 12; i++)
            Seed(db, $"c-att-extra-{i}", JobState.Attention, score: 10, aiScore: 10);

        var list = db.Database.Job.Fetch([], []);
        var states = list.Select(x => x.Job.State).Distinct().ToList();
        Assert.Equal(
        [
            JobState.Attention,
            JobState.AiPending,
            JobState.NotApprovedAI,
            JobState.Applied,
            JobState.NotApprovedRegex,
            JobState.AIError,
            JobState.Saved,
        ], states);

        var attention = list.Where(x => x.Job.State == JobState.Attention).ToList();
        Assert.Equal(12, attention.Count);
        Assert.Equal("c-att-promoted", attention[0].Job.Code);

        var errors = list.Where(x => x.Job.State == JobState.AIError).Select(x => x.Job.Code).ToList();
        Assert.Equal(["c-err-high-regex", "c-err-high-ai"], errors);
    }

    [Fact]
    public void Clean_not_approved_covers_regex_ai_and_error()
    {
        using var db = new GoldenDatabase();
        Seed(db, "cl1", JobState.NotApprovedRegex);
        Seed(db, "cl2", JobState.NotApprovedAI);
        Seed(db, "cl3", JobState.AIError);
        Seed(db, "cl4", JobState.Attention);
        db.ExecuteRaw("UPDATE Job SET RegTime = $old", ("$old", DateTime.Now.AddDays(-10)));

        db.Database.Job.Clean(1);

        Assert.Equal(DBNull.Value, db.Scalar("SELECT Content FROM Job WHERE Code = 'cl1'"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Content FROM Job WHERE Code = 'cl2'"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Content FROM Job WHERE Code = 'cl3'"));
        Assert.Equal("job text", db.Scalar("SELECT Content FROM Job WHERE Code = 'cl4'"));
    }
}
