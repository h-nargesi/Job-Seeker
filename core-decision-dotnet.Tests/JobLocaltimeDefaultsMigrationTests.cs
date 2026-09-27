using System.Data.SQLite;

namespace Photon.JobSeeker.Tests;

public class JobLocaltimeDefaultsMigrationTests
{
    [Fact]
    public void Migration_preserves_rows_and_switches_defaults_to_localtime()
    {
        using var connection = OpenLegacyDatabase();
        SeedLegacyRow(connection);

        Execute(connection, File.ReadAllText(Path.Combine(
            "..", "..", "..", "..",
            "database", "updates", "20260927-02-job-localtime-defaults.sql")));

        var ddl = Assert.IsType<string>(Scalar(connection,
            "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'Job'"));
        Assert.Contains("localtime", ddl);
        Assert.Equal(0L, Scalar(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'Job_new%'"));

        Assert.Equal(1L, Scalar(connection, "SELECT COUNT(*) FROM Job"));
        Assert.Equal(7L, Scalar(connection, "SELECT JobID FROM Job"));
        Assert.Equal("2026-09-20 10:00:00", Timestamp(connection, "RegTime"));
        Assert.Equal("2026-09-21 11:22:33", Timestamp(connection, "ModifiedOn"));
        Assert.Equal("Applied", Scalar(connection, "SELECT State FROM Job"));
        Assert.Equal("legacy text", Scalar(connection, "SELECT Content FROM Job"));

        Execute(connection, @"INSERT INTO Job (AgencyID, Country, Code, State, Url)
VALUES (1, 'NL', 'fresh', 'Saved', 'https://example.com/jobs/fresh')");
        var stored = Assert.IsType<DateTime>(Scalar(connection,
            "SELECT RegTime FROM Job WHERE Code = 'fresh'"));
        Assert.True(Math.Abs((stored - DateTime.Now).TotalSeconds) <= 90,
            $"{stored:O} is not within 90s of local now {DateTime.Now:O}");

        Assert.ThrowsAny<SQLiteException>(() => Execute(connection,
            "INSERT INTO Job (AgencyID, Country, Code, State, Url) VALUES (1, 'NL', 'legacy', 'Saved', 'https://x')"));
    }

    private static string Timestamp(SQLiteConnection connection, string column)
    {
        return Assert.IsType<DateTime>(Scalar(connection, $"SELECT {column} FROM Job"))
            .ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static SQLiteConnection OpenLegacyDatabase()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, @"
CREATE TABLE Agency (
    AgencyID    integer not null    primary key,
    Title       text    not null    unique,
    Active      integer not null    default 3,
    Domain      text    not null,
    Link        text    not null,
    UserName    text        null,
    Password    text        null,
    Settings    text        null
)");
        Execute(connection, @"
CREATE TABLE Job (
	JobID			integer		not null	primary key,
	RegTime			timestamp	not null	default current_timestamp,
	PublishedAt		timestamp		null,
	ModifiedOn		timestamp	not null	default current_timestamp,
	AgencyID		integer 	not null,
	Country			text		not null,
	Code			text		not null,
	Title			text			null,
	State			text		not null,
	Score			integer			null,
	Url				text		not null,
	Html			text		null,
	Content			text		null,
	Link			text		null,
	Log				text		null,
	Options			text		null,
	Tries			text		null,
	Attempts		integer		not null	default 0,
	AiScore			integer			null,
	AiVerdict		text		null,
	AiReason		text		null,
	AiSeniority		text		null,
	AiSalaryMin		integer			null,
	AiSalaryMax		integer			null,
	AiCurrency		text		null,
	AiPeriod		text		null,
	AiWorkModel		text		null,
	AiRelocation	text		null,
	AiContract		text		null,
	AiExperienceYears	integer		null,
	AiSkills		text		null,
	AiOptions		text		null,
	ResumeText		text		null,

	unique			(AgencyID, Code),
	foreign key		(AgencyID) references Agency (AgencyID) on delete no action
)");
        return connection;
    }

    private static void SeedLegacyRow(SQLiteConnection connection)
    {
        Execute(connection, "INSERT INTO Agency (AgencyID, Title, Active, Domain, Link) VALUES (1, 'Legacy', 3, 'example.com', 'https://example.com')");
        Execute(connection, @"INSERT INTO Job (JobID, RegTime, ModifiedOn, AgencyID, Country, Code, Title, State, Score, Url, Content)
VALUES (7, '2026-09-20 10:00:00', '2026-09-21 11:22:33', 1, 'NL', 'legacy', 'Legacy Dev', 'Applied', 90, 'https://example.com/jobs/legacy', 'legacy text')");
    }

    private static object? Scalar(SQLiteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Execute(SQLiteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
