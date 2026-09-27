namespace Photon.JobSeeker.Tests;

public class JobStatsTests
{
    private static string Stamp(DateTime time) => time.ToString("yyyy-MM-dd HH:mm:ss");

    private static void Seed(GoldenDatabase db, string code, JobState state,
        DateTime regTime, DateTime modifiedOn, DateTime? publishedAt = null)
    {
        Seed(db, GoldenDatabase.AgencyId, "NL", code, state, regTime, modifiedOn, publishedAt);
    }

    private static void Seed(GoldenDatabase db, long agencyId, string country, string code, JobState state,
        DateTime regTime, DateTime modifiedOn, DateTime? publishedAt = null)
    {
        db.Database.Job.InsertFromSearch(agencyId, country, $"https://example.com/jobs/{code}", code);
        db.ExecuteRaw($@"
UPDATE Job SET State = '{state}', RegTime = @reg, ModifiedOn = @mod, PublishedAt = @pub
WHERE Code = @code",
            ("reg", Stamp(regTime)),
            ("mod", Stamp(modifiedOn)),
            ("pub", publishedAt == null ? DBNull.Value : (object)Stamp(publishedAt.Value)),
            ("code", code));
    }

    private static void SeedAgency(GoldenDatabase db, long id, string title)
    {
        db.ExecuteRaw($"INSERT INTO Agency (AgencyID, Title, Active, Domain, Link) VALUES ({id}, '{title}', 3, '{title.ToLower()}.com', 'https://{title.ToLower()}.com')");
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

    [Fact]
    public void StatsAgencyYield_counts_and_rates_per_agency()
    {
        using var db = new GoldenDatabase();
        SeedAgency(db, 2, "Other");
        var now = DateTime.Now;

        Seed(db, "sv1", JobState.Saved, now, now);
        Seed(db, "sv2", JobState.Saved, now, now);
        Seed(db, "gr", JobState.NotApprovedRegex, now, now);
        Seed(db, "at", JobState.Attention, now, now);
        Seed(db, "ap", JobState.Applied, now, now);
        Seed(db, 2, "NL", "o-sv", JobState.Saved, now, now);
        Seed(db, 2, "NL", "o-ap", JobState.Applied, now, now);

        var result = db.Database.Job.StatsAgencyYield([]);

        Assert.Equal(2, result.Count);
        var golden = Assert.Single(result, row => row.Title == "Golden");
        Assert.Equal(5, golden.JobCount);
        Assert.Equal(3, golden.Analyzed);
        Assert.Equal(2, golden.Accepted);
        Assert.Equal(1, golden.Applied);
        Assert.Equal(60, golden.AnalyzingRate);
        Assert.Equal(66, golden.AcceptingRate);

        var other = Assert.Single(result, row => row.Title == "Other");
        Assert.Equal(2, other.JobCount);
        Assert.Equal(1, other.Analyzed);
        Assert.Equal(1, other.Accepted);
        Assert.Equal(1, other.Applied);
    }

    [Fact]
    public void StatsAgencyYield_filters_by_agency_title()
    {
        using var db = new GoldenDatabase();
        SeedAgency(db, 2, "Other");
        var now = DateTime.Now;

        Seed(db, "a", JobState.Applied, now, now);
        Seed(db, 2, "NL", "b", JobState.Saved, now, now);

        var result = db.Database.Job.StatsAgencyYield(["Golden"]);

        var golden = Assert.Single(result);
        Assert.Equal("Golden", golden.Title);
        Assert.Equal(1, golden.JobCount);
    }

    [Fact]
    public void StatsFunnel_sums_overall_and_keeps_only_active_agencies()
    {
        using var db = new GoldenDatabase();
        SeedAgency(db, 2, "Empty");
        var now = DateTime.Now;

        Seed(db, "sv", JobState.Saved, now, now);
        Seed(db, "gr", JobState.NotApprovedRegex, now, now);
        Seed(db, "at", JobState.Attention, now, now);
        Seed(db, "ap", JobState.Applied, now, now);

        var funnel = db.Database.Job.StatsFunnel(db.Database.Job.StatsAgencyYield([]));

        Assert.Equal(4, funnel.Overall.Saved);
        Assert.Equal(3, funnel.Overall.Analyzed);
        Assert.Equal(2, funnel.Overall.Attention);
        Assert.Equal(1, funnel.Overall.Applied);

        var agency = Assert.Single(funnel.Agencies);
        Assert.Equal("Golden", agency.Title);
        Assert.Equal(4, agency.Stages.Saved);
        Assert.Equal(3, agency.Stages.Analyzed);
        Assert.Equal(2, agency.Stages.Attention);
        Assert.Equal(1, agency.Stages.Applied);
    }

    [Fact]
    public void StatsPipelineHealth_buckets_pending_and_errors_by_modifiedon_day()
    {
        using var db = new GoldenDatabase();
        SeedAgency(db, 2, "Other");
        var today = DateTime.Now;
        var yesterday = today.AddDays(-1);

        Seed(db, "p1", JobState.AiPending, yesterday, yesterday.AddHours(-2));
        Seed(db, "p2", JobState.AiPending, yesterday, yesterday);
        Seed(db, "e1", JobState.AIError, yesterday, yesterday);
        Seed(db, "e2", JobState.AIError, today, today);
        Seed(db, "old", JobState.AIError, today.AddDays(-40), today.AddDays(-40));
        Seed(db, "ap-ignored", JobState.Applied, yesterday, yesterday);
        Seed(db, 2, "NL", "o-e", JobState.AIError, yesterday, yesterday);

        var result = db.Database.Job.StatsPipelineHealth(30, [], []);

        Assert.Equal(30, result.Count);
        Assert.Equal(Day(today.AddDays(-29)), result[0].Day);

        var yesterdayRow = result.Single(row => row.Day == Day(yesterday));
        Assert.Equal(2, yesterdayRow.AiPending);
        Assert.Equal(2, yesterdayRow.AiError);

        var todayRow = result.Single(row => row.Day == Day(today));
        Assert.Equal(0, todayRow.AiPending);
        Assert.Equal(1, todayRow.AiError);
    }

    [Fact]
    public void StatsPipelineHealth_applies_agency_and_country_filters()
    {
        using var db = new GoldenDatabase();
        SeedAgency(db, 2, "Other");
        var now = DateTime.Now;

        Seed(db, 1, "NL", "nl", JobState.AiPending, now, now);
        Seed(db, 1, "DE", "de", JobState.AiPending, now, now);
        Seed(db, 2, "NL", "other-nl", JobState.AiPending, now, now);

        var byCountry = db.Database.Job.StatsPipelineHealth(30, [], ["NL"]);
        var todayRow = byCountry.Single(row => row.Day == Day(now));
        Assert.Equal(2, todayRow.AiPending);

        var byAgency = db.Database.Job.StatsPipelineHealth(30, ["Golden"], []);
        todayRow = byAgency.Single(row => row.Day == Day(now));
        Assert.Equal(2, todayRow.AiPending);
    }

    [Fact]
    public void StatsPipelineHealth_on_empty_database_zero_fills_window()
    {
        using var db = new GoldenDatabase();

        var result = db.Database.Job.StatsPipelineHealth(30, [], []);

        Assert.Equal(30, result.Count);
        Assert.All(result, row =>
        {
            Assert.Equal(0, row.AiPending);
            Assert.Equal(0, row.AiError);
        });
    }
}
