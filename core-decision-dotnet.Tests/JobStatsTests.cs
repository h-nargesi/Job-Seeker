namespace Photon.JobSeeker.Tests;

public class JobStatsTests
{
    private static string Stamp(DateTime time) => time.ToString("yyyy-MM-dd HH:mm:ss");

    private static void Seed(GoldenDatabase db, string code, JobState state,
        DateTime regTime, DateTime modifiedOn, DateTime? publishedAt = null)
    {
        db.SaveSearchJob(code, $"https://example.com/jobs/{code}");
        db.ExecuteRaw($@"
UPDATE Job SET State = '{state}', RegTime = @reg, ModifiedOn = @mod, PublishedAt = @pub
WHERE Code = @code",
            ("reg", Stamp(regTime)),
            ("mod", Stamp(modifiedOn)),
            ("pub", publishedAt == null ? DBNull.Value : (object)Stamp(publishedAt.Value)),
            ("code", code));
    }

    private static string Day(DateTime date) => date.Date.ToString("yyyy-MM-dd");

    [Fact]
    public void StatsDailyStacked_buckets_all_seven_segments_by_regtime_day()
    {
        using var db = new GoldenDatabase();
        var today = DateTime.Now;

        Seed(db, "s1", JobState.Saved, today.AddHours(-2), today);
        Seed(db, "s2", JobState.Saved, today.AddHours(-3), today);
        Seed(db, "rv", JobState.Revaluation, today.AddHours(-2), today);
        Seed(db, "gr1", JobState.NotApprovedRegex, today.AddHours(-2), today);
        Seed(db, "gr2", JobState.NotApprovedAI, today.AddHours(-2), today);
        Seed(db, "ai1", JobState.AiPending, today.AddHours(-2), today);
        Seed(db, "ai2", JobState.AIError, today.AddHours(-2), today);
        Seed(db, "at", JobState.Attention, today.AddHours(-2), today);
        Seed(db, "ap", JobState.Applied, today.AddHours(-2), today.AddHours(-1));
        Seed(db, "rj", JobState.Rejected, today.AddHours(-2), today.AddHours(-1));
        Seed(db, "old", JobState.Saved, today.AddDays(-5), today.AddDays(-5));

        var result = db.Database.Job.StatsDailyStacked(30);

        Assert.Equal(30, result.Count);
        Assert.Equal(Day(today.AddDays(-29)), result[0].Day);
        Assert.Equal(Day(today), result[^1].Day);

        var todayRow = result.Single(row => row.Day == Day(today));
        Assert.Equal(2, todayRow.Saved);
        Assert.Equal(1, todayRow.Revaluation);
        Assert.Equal(2, todayRow.GateRejected);
        Assert.Equal(2, todayRow.InAi);
        Assert.Equal(1, todayRow.Attention);
        Assert.Equal(1, todayRow.Applied);
        Assert.Equal(1, todayRow.Rejected);

        var oldRow = result.Single(row => row.Day == Day(today.AddDays(-5)));
        Assert.Equal(1, oldRow.Saved);
        Assert.Equal(0, oldRow.Applied);
    }

    [Fact]
    public void StatsDailyStacked_zero_fills_days_without_jobs()
    {
        using var db = new GoldenDatabase();
        Seed(db, "only", JobState.Saved, DateTime.Now.AddHours(-1), DateTime.Now);

        var result = db.Database.Job.StatsDailyStacked(30);

        Assert.Equal(30, result.Count);
        Assert.Equal(29, result.Count(row => row.Saved == 0 && row.Rejected == 0));
        Assert.Equal(1, result.Count(row => row.Saved == 1));
    }

    [Fact]
    public void StatsVelocity_buckets_terminal_states_by_modifiedon_day()
    {
        using var db = new GoldenDatabase();
        var today = DateTime.Now;
        var yesterday = today.AddDays(-1);

        Seed(db, "ap-old-reg", JobState.Applied, today.AddDays(-10), yesterday.AddHours(-1));
        Seed(db, "rj-yes", JobState.Rejected, yesterday, yesterday);
        Seed(db, "ap-today", JobState.Applied, today, today);
        Seed(db, "at-ignored", JobState.Attention, yesterday, yesterday);

        var result = db.Database.Job.StatsVelocity(30);

        Assert.Equal(30, result.Count);

        var yesterdayRow = result.Single(row => row.Day == Day(yesterday));
        Assert.Equal(1, yesterdayRow.Applied);
        Assert.Equal(1, yesterdayRow.Rejected);

        var todayRow = result.Single(row => row.Day == Day(today));
        Assert.Equal(1, todayRow.Applied);
        Assert.Equal(0, todayRow.Rejected);
    }

    [Fact]
    public void StatsKpis_averages_disposition_and_attention_age()
    {
        using var db = new GoldenDatabase();
        var midnight = DateTime.Now.Date;

        Seed(db, "ap-3d", JobState.Applied,
            regTime: midnight.AddDays(-4),
            modifiedOn: midnight,
            publishedAt: midnight.AddDays(-3));
        Seed(db, "rj-1d", JobState.Rejected,
            regTime: midnight.AddDays(-1),
            modifiedOn: midnight);
        Seed(db, "at-5d", JobState.Attention,
            regTime: midnight.AddDays(-5),
            modifiedOn: midnight,
            publishedAt: midnight.AddDays(-5));
        Seed(db, "at-10d", JobState.Attention,
            regTime: midnight.AddDays(-10),
            modifiedOn: midnight);

        var kpis = db.Database.Job.StatsKpis();

        Assert.Equal(2.0, kpis.AvgDispositionDays);
        Assert.Equal(2, kpis.AttentionBacklog);
        Assert.InRange(kpis.AttentionAvgAgeDays!.Value, 7.5, 8.5);
    }

    [Fact]
    public void StatsKpis_on_empty_database_returns_nulls_and_zero_backlog()
    {
        using var db = new GoldenDatabase();

        var kpis = db.Database.Job.StatsKpis();

        Assert.Null(kpis.AvgDispositionDays);
        Assert.Equal(0, kpis.AttentionBacklog);
        Assert.Null(kpis.AttentionAvgAgeDays);
    }
}
