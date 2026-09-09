using System.Data;
using System.Data.SQLite;
using Dapper;

namespace Photon.JobSeeker.Tests;

public class PhaseBNewApiTests
{
    [Fact]
    public void InsertJob_assigns_rowid_on_success()
    {
        using var db = new GoldenDatabase();

        var job = new Job
        {
            AgencyID = GoldenDatabase.AgencyId,
            Country = "NL",
            Code = "new1",
            State = JobState.Saved,
            Url = "https://example.com/new1",
            Title = "Fresh",
        };

        db.Database.Job.InsertJob(job);

        Assert.NotEqual(0, job.JobID);
        Assert.Equal(job.JobID, db.Scalar("SELECT JobID FROM Job"));
        Assert.Equal("Fresh", db.Scalar("SELECT Title FROM Job"));
    }

    [Fact]
    public void InsertJob_conflict_backfills_existing_jobid()
    {
        using var db = new GoldenDatabase();

        var first = new Job
        {
            AgencyID = GoldenDatabase.AgencyId,
            Country = "NL",
            Code = "dup",
            State = JobState.Saved,
            Url = "https://example.com/dup/1",
            Title = "First",
        };
        db.Database.Job.InsertJob(first);

        db.SaveSearchJob("other", "https://example.com/other");

        var second = new Job
        {
            AgencyID = GoldenDatabase.AgencyId,
            Country = "DE",
            Code = "dup",
            State = JobState.Saved,
            Url = "https://example.com/dup/2",
            Title = "Second",
        };
        db.Database.Job.InsertJob(second);

        Assert.Equal(first.JobID, second.JobID);
        Assert.Equal(2L, db.Scalar("SELECT COUNT(*) FROM Job"));
        Assert.Equal("First", db.Scalar("SELECT Title FROM Job WHERE Code = 'dup'"));
        Assert.Equal("NL", db.Scalar("SELECT Country FROM Job WHERE Code = 'dup'"));
    }

    [Fact]
    public void CreateTrend_conflict_backfills_existing_trendid_not_stale_rowid()
    {
        using var db = new GoldenDatabase();

        var first = new Trend { AgencyID = 1, State = TrendState.Seeking };
        db.Database.Trend.CreateTrend(first);

        var other = new Trend { AgencyID = 2, State = TrendState.Seeking };
        db.Database.Trend.CreateTrend(other);

        var conflicted = new Trend { AgencyID = 1, State = TrendState.Seeking };
        db.Database.Trend.CreateTrend(conflicted);

        Assert.NotEqual(first.TrendID, other.TrendID);
        Assert.Equal(first.TrendID, conflicted.TrendID);
        Assert.Equal(2L, db.Scalar("SELECT COUNT(*) FROM Trend"));
        Assert.Equal("Seeking", db.Scalar("SELECT State FROM Trend WHERE AgencyID = 1"));
    }

    [Fact]
    public void Block_by_agency_and_type_conflict_returns_existing_trendid()
    {
        using var db = new GoldenDatabase();

        var existing = new Trend { AgencyID = 1, State = TrendState.Seeking };
        db.Database.Trend.CreateTrend(existing);

        var other = new Trend { AgencyID = 3, State = TrendState.Seeking };
        db.Database.Trend.CreateTrend(other);

        var blocked_id = db.Database.Trend.Block(1, TrendType.Search);

        Assert.Equal(existing.TrendID, blocked_id);
        Assert.Equal(2L, db.Scalar("SELECT COUNT(*) FROM Trend"));
        Assert.Equal("Seeking", db.Scalar("SELECT State FROM Trend WHERE AgencyID = 1"));
    }

    [Fact]
    public void UpdateScrapedJob_base_set_updates_only_title_country_html_content()
    {
        using var db = new GoldenDatabase();
        var job = new Job
        {
            AgencyID = GoldenDatabase.AgencyId,
            Country = "NL",
            Code = "sc1",
            State = JobState.Saved,
            Url = "https://example.com/sc1",
            Title = "Old",
            Score = 11,
            Tries = "1: prev",
        };
        db.Database.Job.InsertJob(job);
        db.ExecuteRaw("UPDATE Job SET Link = 'https://apply.old', Log = 'old log' WHERE Code = 'sc1'");

        job.Title = "New Title";
        job.Country = "DE";
        job.Html = "<html>new</html>";
        job.Content = "new content";
        job.Code = "changed-code";
        job.Link = "https://apply.new";
        job.State = JobState.NotApproved;

        db.Database.Job.UpdateScrapedJob(job, codeChanged: false, linkFound: false, includeState: false);

        Assert.Equal("New Title", db.Scalar("SELECT Title FROM Job"));
        Assert.Equal("DE", db.Scalar("SELECT Country FROM Job"));
        Assert.Equal("<html>new</html>", db.Scalar("SELECT Html FROM Job"));
        Assert.Equal("new content", db.Scalar("SELECT Content FROM Job"));
        Assert.Equal("sc1", db.Scalar("SELECT Code FROM Job"));
        Assert.Equal("https://apply.old", db.Scalar("SELECT Link FROM Job"));
        Assert.Equal("Saved", db.Scalar("SELECT State FROM Job"));
        Assert.Equal(11L, db.Scalar("SELECT Score FROM Job"));
        Assert.Equal("1: prev", db.Scalar("SELECT Tries FROM Job"));
        Assert.Equal("old log", db.Scalar("SELECT Log FROM Job"));
    }

    [Fact]
    public void UpdateScrapedJob_flags_add_exactly_their_columns()
    {
        using var db = new GoldenDatabase();
        var job = new Job
        {
            AgencyID = GoldenDatabase.AgencyId,
            Country = "NL",
            Code = "sc2",
            State = JobState.Saved,
            Url = "https://example.com/sc2",
        };
        db.Database.Job.InsertJob(job);

        job.Code = "sc2-real";
        job.Link = "https://apply.here";
        job.State = JobState.NotApproved;

        db.Database.Job.UpdateScrapedJob(job, codeChanged: true, linkFound: true, includeState: true);

        Assert.Equal("sc2-real", db.Scalar("SELECT Code FROM Job"));
        Assert.Equal("https://apply.here", db.Scalar("SELECT Link FROM Job"));
        Assert.Equal("NotApproved", db.Scalar("SELECT State FROM Job"));
    }

    [Fact]
    public void UpdateStepstoneJob_preserves_tries_null_behavior()
    {
        using var db = new GoldenDatabase();
        var job = new Job
        {
            AgencyID = GoldenDatabase.AgencyId,
            Country = "DE",
            Code = "ss1",
            State = JobState.Saved,
            Url = "https://example.com/ss1",
            Title = "Old",
        };
        db.Database.Job.InsertJob(job);
        db.ExecuteRaw("UPDATE Job SET Tries = '1: prev', Html = '<html>old</html>', Content = 'old' WHERE Code = 'ss1'");

        job.Title = "Stepstone Title";
        job.Html = "<div>stepstone html</div>";
        job.Content = "stepstone content";
        job.Tries = "2: should be dropped";

        db.Database.Job.UpdateStepstoneJob(job);

        Assert.Equal("Stepstone Title", db.Scalar("SELECT Title FROM Job"));
        Assert.Equal("<div>stepstone html</div>", db.Scalar("SELECT Html FROM Job"));
        Assert.Equal("stepstone content", db.Scalar("SELECT Content FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Tries FROM Job"));
    }

    [Fact]
    public void UpdateActivity_keeps_agencyid_and_writes_state_type_lastactivity_reserved()
    {
        using var db = new GoldenDatabase();

        var trend = new Trend { AgencyID = 7, State = TrendState.Seeking };
        db.Database.Trend.CreateTrend(trend);

        trend.State = TrendState.Analyzing;
        trend.Reserved = true;
        trend.LastActivity = DateTime.Now;

        db.Database.Trend.UpdateActivity(trend);

        Assert.Equal(7L, db.Scalar("SELECT AgencyID FROM Trend"));
        Assert.Equal("Analyzing", db.Scalar("SELECT State FROM Trend"));
        Assert.Equal("Job", db.Scalar("SELECT Type FROM Trend"));
        Assert.Equal(true, db.Scalar("SELECT Reserved FROM Trend"));
    }

    [Fact]
    public void Report_projection_handles_trendless_agencies()
    {
        using var db = new GoldenDatabase();
        db.ExecuteRaw("INSERT INTO Agency (AgencyID, Title, Active, Domain, Link) VALUES (2, 'Idle', 0, 'idle.com', 'https://idle.com')");
        db.SaveTrend(1, TrendState.Seeking);

        var report = db.Database.Trend.Report();

        Assert.Equal(2, report.Count);
        var by_agency = report.ToDictionary(r => (string)r.Agency);
        Assert.True((bool)by_agency.ContainsKey("Golden"));
        Assert.True((bool)by_agency.ContainsKey("Idle"));

        var golden = by_agency["Golden"];
        Assert.NotNull((long?)golden.TrendID);
        Assert.Equal("Search", (string?)golden.Type);
        Assert.Equal("Seeking", (string?)golden.State);

        var idle = by_agency["Idle"];
        Assert.Null((long?)idle.TrendID);
        Assert.Equal("Blocked", (string?)idle.State);
        Assert.Equal("-", (string?)idle.LastActivity);
    }

    [Fact]
    public void EnumNameTypeHandler_round_trips_enum_names()
    {
        var handler = new EnumNameTypeHandler<JobState>();

        Assert.Equal(JobState.Saved, handler.Parse("Saved"));
        Assert.Equal(JobState.NotApproved, handler.Parse("NotApproved"));

        var parameter = new SQLiteParameter();
        handler.SetValue(parameter, JobState.Attention);
        Assert.Equal("Attention", parameter.Value);
        Assert.Equal(DbType.String, parameter.DbType);
    }

    [Fact]
    public void ResumeContextTypeHandler_round_trips_json_and_nulls()
    {
        var handler = new ResumeContextTypeHandler();

        var context = new ResumeContext();
        context.Keys["MORE"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Backend" };
        context.JobTitle = "Senior Dev";

        var parameter = new SQLiteParameter();
        handler.SetValue(parameter, context);
        var json = Assert.IsType<string>(parameter.Value);
        Assert.Contains("Backend", json);

        var parsed = handler.Parse(json);
        Assert.Equal("Senior Dev", parsed.JobTitle);
        Assert.Contains("Backend", parsed.Keys["MORE"]!);

        var null_parameter = new SQLiteParameter();
        handler.SetValue(null_parameter, null);
        Assert.Equal(DBNull.Value, null_parameter.Value);
    }

    [Fact]
    public void TypeHandlers_round_trip_through_the_database()
    {
        using var db = new GoldenDatabase();

        var job = new Job
        {
            AgencyID = GoldenDatabase.AgencyId,
            Country = "NL",
            Code = "th1",
            State = JobState.Attention,
            Url = "https://example.com/th1",
            Options = new ResumeContext(),
        };
        job.Options!.Keys["SQL"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sql" };
        db.Database.Job.InsertJob(job);

        Assert.Equal("Attention", db.Scalar("SELECT State FROM Job"));
        Assert.Contains("sql", Assert.IsType<string>(db.Scalar("SELECT Options FROM Job")));

        var fetched = db.Database.Job.Fetch(job.JobID);
        Assert.NotNull(fetched);
        Assert.Equal(JobState.Attention, fetched!.State);
        Assert.NotNull(fetched.Options);
        Assert.Contains("sql", fetched.Options!.Keys["SQL"]!);
    }
}
