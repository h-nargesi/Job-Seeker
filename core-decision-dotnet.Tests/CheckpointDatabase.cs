using System.Data.SQLite;
using System.Text.RegularExpressions;
using Photon.JobSeeker.Analyze.Pages;

namespace Photon.JobSeeker.Tests;

internal sealed class CheckpointAgency : Agency
{
    public TrendState PageState { get; set; } = TrendState.Other;

    public Command[]? PageCommands { get; set; } = [];

    public override string Name => "CheckpointAgency";

    public override Regex? JobAcceptabilityChecker => null;

    public override string SearchLink => "https://cp.example.com/jobs";

    protected override void RunningSearchingMethodChanged(int value) { }

    protected override IEnumerable<Type> GetSubPages()
    {
        yield return typeof(CheckpointStubPage);
    }
}

internal sealed class CheckpointStubPage(Agency parent) : Page(parent)
{
    public override int Order => 100;

    public override TrendState TrendState => ((CheckpointAgency)Parent).PageState;

    public override Command[]? IssueCommand(string url, string content) => ((CheckpointAgency)Parent).PageCommands;
}

internal sealed class CheckpointDatabase : IDisposable
{
    private readonly SQLiteConnection keeper;

    public CheckpointDatabase(int active = 3, bool no_settings = false)
    {
        var connection_string = $"Data Source=file:checkpoint{Guid.NewGuid():N}?mode=memory&cache=shared;Pooling=False";

        keeper = new SQLiteConnection(connection_string);
        keeper.Open();

        Database = new Database(keeper);
        Database.Execute(DDL_AGENCY);
        Database.Execute(DDL_JOB);
        Database.Execute(DDL_TREND);
        Database.Execute(@"
INSERT INTO Agency (AgencyID, Title, Active, Domain, Link, Settings)
VALUES (1, 'CheckpointAgency', @active, 'cp\.example\.com$', 'https://cp.example.com/', @settings)",
            new { active, settings = no_settings ? null : SettingsJson() });

        Analyzer = new Analyzer(new SharedDatabaseFactory(connection_string));
        _ = Analyzer.Agencies;
    }

    public Database Database { get; }

    public Analyzer Analyzer { get; }

    public CheckpointAgency Agency => (CheckpointAgency)Analyzer.Agencies["CheckpointAgency"];

    public CheckpointAgency AgencyById => (CheckpointAgency)Analyzer.AgenciesByID[1];

    public void Dispose()
    {
        Database.Dispose();
    }

    public long CountTrends()
    {
        return Database.ExecuteScalar<long>("SELECT COUNT(*) FROM Trend");
    }

    public void AgeTrend(long agencyId, DateTime lastActivity)
    {
        Database.Execute("UPDATE Trend SET LastActivity = @la WHERE AgencyID = @id",
            new { la = lastActivity, id = agencyId });
    }

    private static string SettingsJson()
    {
        return @"{ ""running"": 0, ""methods"": [
  { ""Title"": ""M0"", ""Url"": ""0"" },
  { ""Title"": ""M1"", ""Url"": ""1"" },
  { ""Title"": ""M2"", ""Url"": ""2"" } ] }";
    }

    private sealed class SharedDatabaseFactory(string connection_string) : IDatabaseFactory
    {
        public Database Open()
        {
            var connection = new SQLiteConnection(connection_string);
            connection.Open();
            return new Database(connection);
        }
    }

    private const string DDL_AGENCY = @"
create table Agency (
    AgencyID    integer     not null    primary key,
    Title       text        not null    unique,
    Active      integer     not null    default 3,
    Domain      text        not null,
    Link        text        not null,
    UserName    text            null,
    Password    text            null,
    Settings    text            null
)";

    private const string DDL_JOB = @"
create table Job (
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
    Attempts    integer     not null    default 0,
    AiScore             integer     null,
    AiVerdict           text        null,
    AiReason            text        null,
    AiSeniority         text        null,
    AiSalaryMin         integer     null,
    AiSalaryMax         integer     null,
    AiCurrency          text        null,
    AiPeriod            text        null,
    AiWorkModel         text        null,
    AiContract          text        null,
    AiExperienceYears   integer     null,
    AiSkills            text        null,
    AiOptions           text        null,
    ResumeText          text        null,
    unique (AgencyID, Code)
)";

    private const string DDL_TREND = @"
create table Trend (
    TrendID         integer     not null    primary key,
    AgencyID        integer     not null,
    Type            text        not null,
    State           text        not null,
    LastActivity    timestamp   not null    default current_timestamp,
    Reserved        bit         not null    default 0,
    unique (AgencyID, Type)
)";
}
