using System.Data.SQLite;

namespace Photon.JobSeeker.Tests;

public class GetFirstJobTests : IDisposable
{
    private readonly Database database;

    public GetFirstJobTests()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();

        database = new Database(connection);
        database.Execute(@"
create table Job (
    JobID       integer     not null    primary key,
    RegTime     timestamp   not null    default current_timestamp,
    ModifiedOn  timestamp   not null    default current_timestamp,
    AgencyID    integer     not null,
    Country     text        not null,
    Code        text        not null,
    Title       text        null,
    State       text        not null,
    Score       integer     null,
    Url         text        not null,
    Html        text        null,
    Content     text        null,
    Link        text        null,
    Log         text        null,
    Options     text        null,
    Tries       text        null,
    Attempts    integer     not null    default 0
)");
    }

    public void Dispose()
    {
        database.Dispose();
    }

    [Fact]
    public void Never_tried_first_then_most_attempted_then_job_id()
    {
        InsertJob(1, "u1", null, 0);
        InsertJob(2, "u2", "1: a", 1);
        InsertJob(3, "u3", "1: a\n2: b", 2);
        InsertJob(4, "u4", "1: a\n2: b\n3: c", 3);

        Assert.Equal("u1", database.Job.GetFirstJob(1));
        Assert.Equal(1, AttemptsOf(1));

        Assert.Equal("u4", database.Job.GetFirstJob(1));
        Assert.Equal("u3", database.Job.GetFirstJob(1));
        Assert.Equal("u3", database.Job.GetFirstJob(1));

        Assert.Equal(4, AttemptsOf(3));
        Assert.Equal(4, AttemptsOf(4));

        Assert.Equal("u1", database.Job.GetFirstJob(1));
    }

    [Fact]
    public void Two_digit_attempt_rows_are_excluded_not_mis_sorted()
    {
        InsertJob(1, "u9", "9: a", 9);
        InsertJob(2, "u10", "10: a", 10);
        InsertJob(3, "u13", "13: a", 13);
        InsertJob(4, "u0", null, 0);

        Assert.Equal("u0", database.Job.GetFirstJob(1));
        Assert.Equal("u0", database.Job.GetFirstJob(1));
        Assert.Equal("u0", database.Job.GetFirstJob(1));
        Assert.Equal("u0", database.Job.GetFirstJob(1));

        Assert.Null(database.Job.GetFirstJob(1));

        Assert.Equal(9, AttemptsOf(1));
        Assert.Equal(10, AttemptsOf(2));
        Assert.Equal(13, AttemptsOf(3));
        Assert.Equal(4, AttemptsOf(4));
    }

    [Fact]
    public void Cap_is_exact_four()
    {
        InsertJob(1, "u4", "4: a", 4);
        InsertJob(2, "u14", "14: a", 14);
        InsertJob(3, "u3", "3: a", 3);

        Assert.Equal("u3", database.Job.GetFirstJob(1));
        Assert.Equal(4, AttemptsOf(3));

        Assert.Null(database.Job.GetFirstJob(1));
        Assert.Equal(4, AttemptsOf(1));
        Assert.Equal(14, AttemptsOf(2));
    }

    [Fact]
    public void RegisterAttempt_sets_attempts_and_prepends_log()
    {
        InsertJob(1, "u1", "2: a\n1: b", 2);

        database.Job.RegisterAttempt(1, 3, "3: new\n2: a\n1: b");

        Assert.Equal(3, AttemptsOf(1));
        Assert.Equal("3: new\n2: a\n1: b", TriesOf(1));
    }

    [Fact]
    public void GetFirstJob_prepends_newest_try_line()
    {
        InsertJob(1, "u1", "1: old", 1);

        Assert.Equal("u1", database.Job.GetFirstJob(1));
        Assert.Equal(2, AttemptsOf(1));

        var lines = TriesOf(1)!.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("2: ", lines[0]);
        Assert.Equal("1: old", lines[1]);
    }

    [Fact]
    public void GetFirstJob_joins_ambient_immediate_transaction()
    {
        InsertJob(1, "u1", null, 0);

        database.BeginTransaction(immediate: true);
        var url = database.Job.GetFirstJob(1);
        database.Commit();

        Assert.Equal("u1", url);
        Assert.Equal(1, AttemptsOf(1));
    }

    [Fact]
    public void Stepstone_reset_reopens_the_job()
    {
        InsertJob(1, "u1", "4: a", 4);

        database.Job.UpdateStepstoneJob(new Job { JobID = 1, Title = "t", Html = "h", Content = "c" });

        Assert.Equal("u1", database.Job.GetFirstJob(1));
        Assert.Equal(1, AttemptsOf(1));
        Assert.StartsWith("1: ", TriesOf(1));
    }

    private void InsertJob(long id, string url, string? tries, int attempts, long agency = 1)
    {
        database.Execute(@"
INSERT INTO Job (JobID, AgencyID, Country, Code, State, Url, Tries, Attempts)
VALUES (@id, @agency, 'DE', @code, @state, @url, @tries, @attempts)",
            new { id, agency, code = $"c{id}", state = nameof(JobState.Saved), url, tries, attempts });
    }

    private long AttemptsOf(long id)
    {
        return database.ExecuteScalar<long>("SELECT Attempts FROM Job WHERE JobID = @id", new { id });
    }

    private string? TriesOf(long id)
    {
        return database.ExecuteScalar<string?>("SELECT Tries FROM Job WHERE JobID = @id", new { id });
    }
}
