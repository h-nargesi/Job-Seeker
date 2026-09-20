namespace Photon.JobSeeker.Tests;

public class TrendsCheckpointTests
{
    private static Result Check(CheckpointDatabase db, Result result)
    {
        return new TrendsCheckpoint(db.Analyzer, db.Database, result).CheckCurrentTrends();
    }

    [Fact]
    public void Idle_agency_gets_a_reserved_search_trend_and_an_open_command()
    {
        using var db = new CheckpointDatabase();

        var result = Check(db, new Result());

        Assert.Equal(2, result.Commands.Length);
        Assert.Equal("open", result.Commands[0].Action);
        Assert.Equal("https://cp.example.com/jobs", result.Commands[0].Params!["url"]);
        Assert.Equal("close", result.Commands[^1].Action);

        var search = db.Database.Trend.Get(1, TrendType.Search);
        Assert.NotNull(search);
        Assert.Equal(TrendState.Seeking, search!.State);
        Assert.True(search.Reserved);
        Assert.Null(db.Database.Trend.Get(1, TrendType.Job));
    }

    [Fact]
    public void Idle_agency_with_a_saved_job_opens_the_search_first_and_records_the_try()
    {
        using var db = new CheckpointDatabase();
        db.Database.Job.InsertFromSearch(1, "NL", "https://cp.example.com/jobs/j1", "j1");

        var result = Check(db, new Result());

        Assert.Equal(2, result.Commands.Length);
        Assert.Equal("open", result.Commands[0].Action);
        Assert.Equal("https://cp.example.com/jobs", result.Commands[0].Params!["url"]);

        var tries = db.Database.ExecuteScalar<string?>("SELECT Tries FROM Job WHERE Code = 'j1'");
        Assert.StartsWith("1: ", tries);

        Assert.Equal(1L, db.CountTrends());
        Assert.NotNull(db.Database.Trend.Get(1, TrendType.Search));
    }

    [Fact]
    public void Not_seeking_agency_gets_search_blocked_and_opens_the_next_job()
    {
        using var db = new CheckpointDatabase(active: 2);
        db.Database.Job.InsertFromSearch(1, "NL", "https://cp.example.com/jobs/j2", "j2");

        var result = Check(db, new Result());

        Assert.Equal(2, result.Commands.Length);
        Assert.Equal("open", result.Commands[0].Action);
        Assert.Equal("https://cp.example.com/jobs/j2", result.Commands[0].Params!["url"]);
        Assert.Equal("close", result.Commands[^1].Action);

        var search = db.Database.Trend.Get(1, TrendType.Search);
        Assert.NotNull(search);
        Assert.Equal(TrendState.Blocked, search!.State);

        var job = db.Database.Trend.Get(1, TrendType.Job);
        Assert.NotNull(job);
        Assert.Equal(TrendState.Analyzing, job!.State);
        Assert.True(job.Reserved);
    }

    [Fact]
    public void Matched_analyzing_result_adopts_the_db_trend_and_goes_to_the_next_job()
    {
        using var db = new CheckpointDatabase();
        db.Database.Job.InsertFromSearch(1, "NL", "https://cp.example.com/jobs/j3", "j3");
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 1, State = TrendState.Analyzing, Reserved = true });
        db.AgeTrend(1, DateTime.Now.AddDays(-1));

        var result = Check(db, new Result { AgencyID = 1, State = TrendState.Analyzing, Commands = [] });

        Assert.Equal(2, result.Commands.Length);
        Assert.Equal("open", result.Commands[0].Action);
        Assert.Equal("https://cp.example.com/jobs", result.Commands[0].Params!["url"]);
        Assert.Equal("go", result.Commands[1].Action);
        Assert.Equal("https://cp.example.com/jobs/j3", result.Commands[1].Params!["url"]);

        var job = db.Database.Trend.Get(1, TrendType.Job);
        Assert.NotNull(job);
        Assert.Equal(TrendState.Analyzing, job!.State);
        Assert.False(job.Reserved);
        Assert.Equal(job.TrendID, result.TrendID);
    }

    [Fact]
    public void Matched_result_without_a_db_trend_regenerates_one_and_closes()
    {
        using var db = new CheckpointDatabase();

        var result = Check(db, new Result { AgencyID = 1, TrendID = 4321, State = TrendState.Analyzing, Commands = [] });

        Assert.Equal(2, result.Commands.Length);
        Assert.Equal("open", result.Commands[0].Action);
        Assert.Equal("close", result.Commands[^1].Action);

        var job = db.Database.Trend.Get(1, TrendType.Job);
        Assert.NotNull(job);
        Assert.Equal(TrendState.Analyzing, job!.State);
        Assert.False(job.Reserved);
        Assert.Equal(job.TrendID, result.TrendID);
    }

    [Fact]
    public void Sweep_removes_stale_trends_and_expired_reservations_but_keeps_fresh_auth()
    {
        using var db = new CheckpointDatabase();
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 2, State = TrendState.Seeking });
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 3, State = TrendState.Analyzing, Reserved = true });
        db.Database.Trend.CreateTrend(new Trend { AgencyID = 4, State = TrendState.Auth });
        db.AgeTrend(2, DateTime.Now.AddMinutes(-10));
        db.AgeTrend(3, DateTime.Now.AddSeconds(-60));
        db.AgeTrend(4, DateTime.Now.AddMinutes(-7));

        Check(db, new Result());

        Assert.Equal(0L, db.Database.ExecuteScalar<long>("SELECT COUNT(*) FROM Trend WHERE AgencyID = 2"));
        Assert.Equal(0L, db.Database.ExecuteScalar<long>("SELECT COUNT(*) FROM Trend WHERE AgencyID = 3"));
        Assert.Equal(1L, db.Database.ExecuteScalar<long>("SELECT COUNT(*) FROM Trend WHERE AgencyID = 4"));
    }

    [Theory]
    [InlineData(TrendState.Blocked, TrendType.Blocked)]
    [InlineData(TrendState.Finished, TrendType.Blocked)]
    [InlineData(TrendState.Auth, TrendType.Login)]
    [InlineData(TrendState.Login, TrendType.Login)]
    [InlineData(TrendState.Seeking, TrendType.Search)]
    [InlineData(TrendState.Analyzing, TrendType.Job)]
    public void Trend_state_maps_to_the_documented_trend_type(TrendState state, TrendType type)
    {
        Assert.Equal(type, state.GetTrendType());
    }

    [Fact]
    public void Trend_type_maps_back_to_its_workflow_state()
    {
        Assert.Equal(TrendState.Blocked, TrendType.Blocked.GetTrendState());
        Assert.Equal(TrendState.Auth, TrendType.Login.GetTrendState());
        Assert.Equal(TrendState.Seeking, TrendType.Search.GetTrendState());
        Assert.Equal(TrendState.Analyzing, TrendType.Job.GetTrendState());
    }
}
