namespace Photon.JobSeeker.Tests;

public class ChallengeHoldTests
{
    private static Result Hold(CheckpointDatabase db, long? trendId, string url)
    {
        return new TrendsCheckpoint(db.Analyzer, db.Database, new Result())
            .HoldForChallenge(db.Agency, trendId, url);
    }

    private static Result Check(CheckpointDatabase db, Result result)
    {
        return new TrendsCheckpoint(db.Analyzer, db.Database, result).CheckCurrentTrends();
    }

    [Fact]
    public void Hold_adopts_the_bound_trend_and_flags_it_without_commands()
    {
        using var db = new CheckpointDatabase();
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Seeking });
        var trend_id = db.Database.Trend.Get(1, TrendType.Search)!.TrendID;

        var result = Hold(db, trend_id, "https://cp.example.com/jobs/123");

        Assert.Empty(result.Commands);
        Assert.Equal(trend_id, result.TrendID);
        Assert.Equal(1, result.AgencyID);

        var row = db.Database.Trend.Get(1, TrendType.Search);
        Assert.NotNull(row);
        Assert.Equal(TrendState.Seeking, row!.State);
        Assert.True(row.Challenge);
    }

    [Fact]
    public void Hold_ignores_a_foreign_trend_id_and_adopts_the_agency_trend()
    {
        using var db = new CheckpointDatabase();
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 2, State = TrendState.Seeking });
        var foreign_id = db.Database.Trend.Get(2, TrendType.Search)!.TrendID;
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Seeking });
        var own_id = db.Database.Trend.Get(1, TrendType.Search)!.TrendID;

        var result = Hold(db, foreign_id, "https://cp.example.com/jobs");

        Assert.Equal(own_id, result.TrendID);
        Assert.True(db.Database.Trend.Get(1, TrendType.Search)!.Challenge);
        Assert.False(db.Database.Trend.Get(2, TrendType.Search)!.Challenge);
    }

    [Fact]
    public void Hold_falls_back_to_the_latest_holdable_trend_for_a_stale_id()
    {
        using var db = new CheckpointDatabase();
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Analyzing });

        var result = Hold(db, 987_654, "https://cp.example.com/jobs/j9");

        var row = db.Database.Trend.Get(1, TrendType.Job);
        Assert.NotNull(row);
        Assert.Equal(row!.TrendID, result.TrendID);
        Assert.True(row.Challenge);
        Assert.Empty(result.Commands);
    }

    [Fact]
    public void Hold_without_any_trend_creates_a_search_trend_for_the_search_link()
    {
        using var db = new CheckpointDatabase();

        var result = Hold(db, null, "https://cp.example.com/jobs?start=0");

        var row = db.Database.Trend.Get(1, TrendType.Search);
        Assert.NotNull(row);
        Assert.Equal(TrendState.Seeking, row!.State);
        Assert.True(row.Challenge);
        Assert.False(row.Reserved);
        Assert.Equal(row.TrendID, result.TrendID);
        Assert.Empty(result.Commands);
    }

    [Fact]
    public void Hold_without_any_trend_creates_a_login_trend_for_other_urls()
    {
        using var db = new CheckpointDatabase();

        var result = Hold(db, null, "https://cp.example.com/signin");

        var row = db.Database.Trend.Get(1, TrendType.Login);
        Assert.NotNull(row);
        Assert.Equal(TrendState.Auth, row!.State);
        Assert.True(row.Challenge);
        Assert.Equal(row.TrendID, result.TrendID);
    }

    [Fact]
    public void A_normal_checkpoint_pass_after_the_hold_clears_the_flag()
    {
        using var db = new CheckpointDatabase();
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Seeking });
        var trend_id = db.Database.Trend.Get(1, TrendType.Search)!.TrendID;
        db.Database.Trend.MarkChallenge(trend_id);
        Assert.True(db.Database.Trend.Get(1, TrendType.Search)!.Challenge);

        var result = Check(db, new Result { AgencyID = 1, TrendID = trend_id, State = TrendState.Seeking, Commands = [] });

        Assert.Equal(trend_id, result.TrendID);
        Assert.False(db.Database.Trend.Get(1, TrendType.Search)!.Challenge);
    }

    [Fact]
    public void Job_page_after_a_released_hold_adopts_the_trend_and_navigates_to_the_next_job()
    {
        using var db = new CheckpointDatabase(active: 2);
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Auth });
        db.Database.Trend.MarkChallenge(db.Database.Trend.Get(1, TrendType.Login)!.TrendID);
        db.Database.Job.InsertFromSearch(1, "NL", "https://cp.example.com/jobs/j5", "j5");

        var result = Check(db, new Result { AgencyID = 1, State = TrendState.Analyzing, Commands = [] });

        var job = db.Database.Trend.Get(1, TrendType.Job);
        Assert.NotNull(job);
        Assert.Equal(TrendState.Analyzing, job!.State);
        Assert.False(job.Challenge);
        Assert.Equal(job.TrendID, result.TrendID);
        Assert.Null(db.Database.Trend.Get(1, TrendType.Login));

        Assert.Single(result.Commands);
        Assert.Equal("go", result.Commands[0].Action);
        Assert.Equal("https://cp.example.com/jobs/j5", result.Commands[0].Params!["url"]);
    }

    [Fact]
    public void Sweep_keeps_a_challenged_trend_until_thirty_minutes()
    {
        using var db = new CheckpointDatabase();
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Seeking });
        db.Database.Trend.MarkChallenge(db.Database.Trend.Get(1, TrendType.Search)!.TrendID);

        db.AgeTrend(1, DateTime.Now.AddMinutes(-10));
        db.Database.Trend.DeleteExpired();
        Assert.Equal(1L, db.CountTrends());

        db.AgeTrend(1, DateTime.Now.AddMinutes(-31));
        db.Database.Trend.DeleteExpired();
        Assert.Equal(0L, db.CountTrends());
    }

    [Fact]
    public void Report_shows_the_challenge_overlay_for_flagged_trends()
    {
        using var db = new CheckpointDatabase();
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Seeking });
        db.Database.Trend.MarkChallenge(db.Database.Trend.Get(1, TrendType.Search)!.TrendID);

        var item = db.Database.Trend.Report().Single(r => r.Agency == "CheckpointAgency");

        Assert.Equal("Challenge", item.State);
        Assert.Equal(TrendType.Search.ToString(), item.Type);
    }

    [Fact]
    public void MigrateChallengeColumn_adds_the_column_to_a_legacy_table()
    {
        using var db = new CheckpointDatabase();
        db.Database.Execute("DROP TABLE Trend");
        db.Database.Execute(@"
CREATE TABLE Trend (
    TrendID         integer     not null    primary key,
    AgencyID        integer     not null,
    Type            text        not null,
    State           text        not null,
    LastActivity    timestamp   not null    default current_timestamp,
    Reserved        bit         not null    default 0,
    unique (AgencyID, Type))");

        TrendBusiness.MigrateChallengeColumn(db.Database);
        TrendBusiness.MigrateChallengeColumn(db.Database);

        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Seeking, Challenge = true });
        Assert.True(db.Database.Trend.Get(1, TrendType.Search)!.Challenge);
    }
}
