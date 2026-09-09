using System.Data.SQLite;

namespace Photon.JobSeeker.Tests;

internal sealed class GoldenDatabase : IDisposable
{
    public const long AgencyId = 1;

    public Database Database { get; }

    public SQLiteConnection Connection { get; }

    public GoldenDatabase()
    {
        Connection = new SQLiteConnection("Data Source=:memory:");
        Connection.Open();
        ExecuteRaw(DDL_AGENCY);
        ExecuteRaw(DDL_JOB);
        ExecuteRaw(DDL_TREND);
        ExecuteRaw("INSERT INTO Agency (AgencyID, Title, Active, Domain, Link) VALUES (1, 'Golden', 3, 'example.com', 'https://example.com')");
        Database = new Database(Connection);
    }

    public void Dispose()
    {
        Connection.Dispose();
        GC.SuppressFinalize(this);
    }

    public object? Scalar(string sql)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public void ExecuteRaw(string sql, params (string name, object value)[] parameters)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        command.ExecuteNonQuery();
    }

    public void SaveSearchJob(string code, string url)
    {
        Database.Job.InsertFromSearch(AgencyId, "NL", url, code);
    }

    public void SaveTrend(long agencyId, TrendState state)
    {
        Database.Trend.CreateTrend(new Trend { AgencyID = agencyId, State = state });
    }

    public long JobId(string code)
    {
        return (long)Scalar($"SELECT JobID FROM Job WHERE Code = '{code}'")!;
    }

    private const string DDL_AGENCY = @"
CREATE TABLE Agency (
    AgencyID    integer not null    primary key,
    Title       text    not null    unique,
    Active      integer not null    default 3,
    Domain      text    not null,
    Link        text    not null,
    UserName    text        null,
    Password    text        null,
    Settings    text        null
)";

    private const string DDL_JOB = @"
CREATE TABLE Job (
    JobID       integer     not null    primary key,
    RegTime     timestamp   not null    default current_timestamp,
    ModifiedOn  timestamp   not null    default current_timestamp,
    AgencyID    integer     not null,
    Country     text        not null,
    Code        text        not null,
    Title       text            null,
    State       text        not null,
    Score       integer         null,
    Url         text        not null,
    Html        text            null,
    Content     text            null,
    Link        text            null,
    Log         text            null,
    Options     text            null,
    Tries       text            null,
    unique (AgencyID, Code)
)";

    private const string DDL_TREND = @"
CREATE TABLE Trend (
    TrendID         integer     not null    primary key,
    AgencyID        integer     not null,
    Type            text        not null,
    State           text        not null,
    LastActivity    timestamp   not null    default current_timestamp,
    Reserved        bit         not null    default 0,
    unique (AgencyID, Type)
)";
}

public class PhaseAGoldenTests
{
    private static JobEligibilityHelper MakeHelper(GoldenDatabase db, params JobOption[] options)
    {
        return new JobEligibilityHelper(
            EligibilityFixture.CreateDictionaries("alpha", "beta"), db.Database, options);
    }

    [Fact]
    public void SearchInsert_persists_job_with_saved_state_text()
    {
        using var db = new GoldenDatabase();

        db.SaveSearchJob("abc123", "https://example.com/jobs/abc123");

        Assert.Equal(1L, db.Scalar("SELECT COUNT(*) FROM Job"));
        Assert.Equal("Saved", db.Scalar("SELECT State FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Title FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Html FROM Job"));
        Assert.NotNull(db.Scalar("SELECT RegTime FROM Job"));
        Assert.NotNull(db.Scalar("SELECT ModifiedOn FROM Job"));
    }

    [Fact]
    public void DuplicateSearchInsert_is_ignored()
    {
        using var db = new GoldenDatabase();

        db.SaveSearchJob("dup", "https://example.com/jobs/dup/1");
        db.SaveSearchJob("dup", "https://example.com/jobs/dup/2");

        Assert.Equal(1L, db.Scalar("SELECT COUNT(*) FROM Job"));
        Assert.Equal("https://example.com/jobs/dup/1", db.Scalar("SELECT Url FROM Job"));
    }

    [Fact]
    public void EvaluateJobEligibility_eligible_job_becomes_attention_with_score_log_options()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("elig1", "https://example.com/jobs/elig1");
        db.ExecuteRaw("UPDATE Job SET Html = '<html><body>raw</body></html>', Content = 'backend alpha beta' WHERE Code = 'elig1'");
        var job = db.Database.Job.Fetch(GoldenDatabase.AgencyId, "elig1");
        Assert.NotNull(job);
        Assert.Equal("<html><body>raw</body></html>", job!.Html);

        var helper = MakeHelper(db, EligibilityFixture.Option("field", 100, "backend", "Backend"));
        var state = helper.EvaluateJobEligibility(job, null);

        Assert.Equal(JobState.Attention, state);
        Assert.Equal("Attention", db.Scalar("SELECT State FROM Job"));
        Assert.Equal(100L, db.Scalar("SELECT Score FROM Job"));

        var log = Assert.IsType<string>(db.Scalar("SELECT Log FROM Job"));
        Assert.Contains("English: (66%)", log);
        Assert.Contains("*Field:*", log);

        Assert.Equal("<html><body>raw</body></html>", db.Scalar("SELECT Html FROM Job"));
        Assert.Equal("backend alpha beta", db.Scalar("SELECT Content FROM Job"));

        var options = db.Database.Job.FetchOptions(job.JobID);
        Assert.NotNull(options);
        Assert.True(options!.Keys.ContainsKey("MORE"));
        Assert.Contains("Backend", options.Keys["MORE"]!);
    }

    [Fact]
    public void EvaluateJobEligibility_language_mismatch_rejects_and_nulls_html_content()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("elig2", "https://example.com/jobs/elig2");
        db.ExecuteRaw("UPDATE Job SET Html = '<html>to be dropped</html>', Content = 'سلام دنیا این یک متن فارسی است' WHERE Code = 'elig2'");
        var job = db.Database.Job.Fetch(GoldenDatabase.AgencyId, "elig2");
        Assert.NotNull(job);
        Assert.NotNull(job!.Content);

        var helper = MakeHelper(db, EligibilityFixture.Option("field", 100, "backend", "Backend"));
        var state = helper.EvaluateJobEligibility(job, null);

        Assert.Equal(JobState.NotApproved, state);
        Assert.Equal("NotApproved", db.Scalar("SELECT State FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Html FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Content FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Score FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Options FROM Job"));
    }

    [Fact]
    public void EvaluateJobEligibility_expired_job_logs_expired_and_rejects()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("elig3", "https://example.com/jobs/elig3");
        db.ExecuteRaw("UPDATE Job SET Html = '<html>gone</html>', Content = 'backend alpha beta no longer accepting applications' WHERE Code = 'elig3'");
        var job = db.Database.Job.Fetch(GoldenDatabase.AgencyId, "elig3");
        Assert.NotNull(job);

        var helper = MakeHelper(db, EligibilityFixture.Option("field", 100, "backend", "Backend"));
        var state = helper.EvaluateJobEligibility(job, new System.Text.RegularExpressions.Regex("no longer accepting"));

        Assert.Equal(JobState.NotApproved, state);
        Assert.StartsWith("Expired!", Assert.IsType<string>(db.Scalar("SELECT Log FROM Job")));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Html FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Content FROM Job"));
    }

    [Fact]
    public void ChangeState_updates_only_state_and_modifiedon()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("cs1", "https://example.com/jobs/cs1");
        db.ExecuteRaw("UPDATE Job SET Title = 'Senior Dev', Tries = '1: prev', Html = '<html>h</html>', Score = 55 WHERE Code = 'cs1'");
        var before = ModifiedOn(db);

        Thread.Sleep(1100);
        db.Database.Job.ChangeState(db.JobId("cs1"), JobState.Applied);

        Assert.Equal("Applied", db.Scalar("SELECT State FROM Job"));
        Assert.Equal("Senior Dev", db.Scalar("SELECT Title FROM Job"));
        Assert.Equal("1: prev", db.Scalar("SELECT Tries FROM Job"));
        Assert.Equal("<html>h</html>", db.Scalar("SELECT Html FROM Job"));
        Assert.Equal(55L, db.Scalar("SELECT Score FROM Job"));
        Assert.NotEqual(before, ModifiedOn(db));
    }

    private static string ModifiedOn(GoldenDatabase db)
    {
        return Assert.IsType<DateTime>(db.Scalar("SELECT ModifiedOn FROM Job")).ToString("yyyy-MM-dd HH:mm:ss");
    }

    [Fact]
    public void RemoveHtmlContent_nulls_only_html_and_content()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("rh1", "https://example.com/jobs/rh1");
        db.ExecuteRaw(@"UPDATE Job SET Title = 'Keep Me', Tries = '1: prev', Score = 42, State = 'Attention', Html = '<html>x</html>', Content = 'text' WHERE Code = 'rh1'");
        var before = ModifiedOn(db);

        Thread.Sleep(1100);
        db.Database.Job.RemoveHtmlContent(db.JobId("rh1"));

        Assert.Equal(DBNull.Value, db.Scalar("SELECT Html FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT Content FROM Job"));
        Assert.Equal("Keep Me", db.Scalar("SELECT Title FROM Job"));
        Assert.Equal("1: prev", db.Scalar("SELECT Tries FROM Job"));
        Assert.Equal(42L, db.Scalar("SELECT Score FROM Job"));
        Assert.Equal("Attention", db.Scalar("SELECT State FROM Job"));
        Assert.NotEqual(before, ModifiedOn(db));
    }

    [Fact]
    public void ChangeOptions_with_null_writes_real_null()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("co1", "https://example.com/jobs/co1");
        db.ExecuteRaw("UPDATE Job SET Options = '{\"Length\": 1}' WHERE Code = 'co1'");

        db.Database.Job.ChangeOptions(db.JobId("co1"), null);

        Assert.Equal(DBNull.Value, db.Scalar("SELECT Options FROM Job"));
    }

    [Fact]
    public void GetFirstJob_returns_url_and_appends_tries_with_age()
    {
        using var db = new GoldenDatabase();
        var url = "https://example.com/jobs/first";
        db.SaveSearchJob("first", url);
        db.ExecuteRaw("UPDATE Job SET RegTime = $reg WHERE Code = 'first'", ("$reg", DateTime.Now.AddDays(-2)));

        var returned = db.Database.Job.GetFirstJob(GoldenDatabase.AgencyId);

        Assert.Equal(url, returned);
        var tries = Assert.IsType<string>(db.Scalar("SELECT Tries FROM Job"));
        Assert.Matches(@"^1: .+ \(age 2d\)$", tries);

        db.Database.Job.GetFirstJob(GoldenDatabase.AgencyId);

        var second = Assert.IsType<string>(db.Scalar("SELECT Tries FROM Job"));
        Assert.StartsWith("2: ", second);
        Assert.Contains("\n1: ", second);
        Assert.Equal("Saved", db.Scalar("SELECT State FROM Job"));
    }

    [Fact]
    public void FetchFrom_marks_job_as_revaluation()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("ff1", "https://example.com/jobs/ff1");
        db.ExecuteRaw("UPDATE Job SET Content = 'some text' WHERE Code = 'ff1'");

        var job = db.Database.Job.FetchFrom(DateTime.Now.AddDays(1));

        Assert.NotNull(job);
        Assert.Equal("ff1", job!.Code);
        Assert.Equal("Revaluation", db.Scalar("SELECT State FROM Job"));
    }

    [Fact]
    public void Transaction_rollback_leaves_row_absent()
    {
        using var db = new GoldenDatabase();

        db.Database.BeginTransaction();
        db.SaveSearchJob("rb1", "https://example.com/jobs/rb1");
        Assert.Equal(1L, db.Scalar("SELECT COUNT(*) FROM Job"));
        db.Database.Rollback();

        Assert.Equal(0L, db.Scalar("SELECT COUNT(*) FROM Job"));
    }

    [Fact]
    public void Trend_block_flows_write_state_as_text()
    {
        using var db = new GoldenDatabase();

        db.Database.Trend.Block(GoldenDatabase.AgencyId, TrendType.Search);

        Assert.Equal(1L, db.Scalar("SELECT COUNT(*) FROM Trend"));
        Assert.Equal("Blocked", db.Scalar("SELECT State FROM Trend"));
        Assert.Equal("Search", db.Scalar("SELECT Type FROM Trend"));

        var blocked = db.Database.Trend.Get(GoldenDatabase.AgencyId, TrendType.Search);
        Assert.NotNull(blocked);
        Assert.Equal(TrendState.Blocked, blocked!.State);
    }

    [Fact]
    public void Trend_block_by_id_updates_state_only()
    {
        using var db = new GoldenDatabase();
        db.SaveTrend(GoldenDatabase.AgencyId, TrendState.Seeking);
        db.ExecuteRaw("UPDATE Trend SET Reserved = 1, LastActivity = $la WHERE AgencyID = 1", ("$la", DateTime.Now.AddDays(-1)));
        var trend_id = (long)db.Scalar("SELECT TrendID FROM Trend")!;

        db.Database.Trend.Block(trend_id);

        Assert.Equal("Blocked", db.Scalar("SELECT State FROM Trend"));
        Assert.Equal(true, db.Scalar("SELECT Reserved FROM Trend"));
        var activity = Assert.IsType<DateTime>(db.Scalar("SELECT LastActivity FROM Trend"));
        Assert.Equal(DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"), activity.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    [Fact]
    public void Trend_delete_expired_removes_only_stale_rows()
    {
        using var db = new GoldenDatabase();
        db.SaveTrend(1, TrendState.Seeking);
        db.SaveTrend(2, TrendState.Seeking);
        db.ExecuteRaw("UPDATE Trend SET LastActivity = $fresh WHERE AgencyID = 1", ("$fresh", DateTime.Now));
        db.ExecuteRaw("UPDATE Trend SET LastActivity = $stale WHERE AgencyID = 2", ("$stale", DateTime.Now.AddMinutes(-10)));

        db.Database.Trend.DeleteExpired();

        Assert.Equal(1L, db.Scalar("SELECT COUNT(*) FROM Trend"));
        Assert.Equal(1L, db.Scalar("SELECT AgencyID FROM Trend"));
    }

    [Fact]
    public void Fetch_dashboard_projection_keeps_job_relocation_agencyname()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("i1", "https://example.com/jobs/i1");
        db.SaveSearchJob("i2", "https://example.com/jobs/i2");
        db.ExecuteRaw("UPDATE Job SET Title = 'T1', Score = 150, State = 'Attention' WHERE Code = 'i1'");

        var list = db.Database.Job.Fetch([], []);

        Assert.Equal(2, list.Count);
        var top = list[0];
        Assert.Equal("Golden", top.AgencyName);
        Assert.False(top.Relocation);
        Assert.NotNull(top.Job);
    }
}
