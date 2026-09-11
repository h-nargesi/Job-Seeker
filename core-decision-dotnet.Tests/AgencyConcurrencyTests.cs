using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Tests;

internal sealed class TestAgency : Agency
{
    public override string Name => "TestAgency";

    public override Regex? JobAcceptabilityChecker => null;

    public override string SearchLink => "https://test/jobs";

    protected override void RunningSearchingMethodChanged(int value) { }

    protected override IEnumerable<Type> GetSubPages()
    {
        yield return typeof(TestSearchPage);
    }
}

internal sealed class TestSearchPage(Agency parent) : SearchPage(parent)
{
    public override Command[]? IssueCommand(string url, string content) => [];

    protected override bool CheckInvalidUrl(string url, string content) => false;

    protected override bool CheckInvalidSearchTitle(string url, string content, out Command[]? commands)
    {
        commands = null;
        return false;
    }

    protected override IEnumerable<(string url, string code)> GetJobUrls(string content) => [];

    protected override Command[] CheckNextButton(string url, string content) => [];
}

public class AgencyConcurrencyTests : IDisposable
{
    private readonly Database database;
    private readonly TestAgency agency;

    public AgencyConcurrencyTests()
    {
        var connection_string = $"Data Source=file:agencylock{Guid.NewGuid():N}?mode=memory&cache=shared;Pooling=False";

        var keeper = new SQLiteConnection(connection_string);
        keeper.Open();

        database = new Database(keeper);

        database.Execute(@"
create table Agency (
    AgencyID    integer     not null    primary key,
    Title       text        not null    unique,
    Active      integer     not null    default 3,
    Domain      text        not null,
    Link        text        not null,
    UserName    text        null,
    Password    text        null,
    Settings    text        null
)");

        database.Execute(@"
create table Trend (
    TrendID         integer     not null    primary key,
    AgencyID        integer     not null,
    Type            text        not null,
    State           text        not null,
    LastActivity    timestamp   not null    default current_timestamp,
    Reserved        bit         not null    default 0,
    unique (AgencyID, Type)
)");

        database.Execute(@"
INSERT INTO Agency (AgencyID, Title, Active, Domain, Link, Settings)
VALUES (1, 'TestAgency', 1, 'test\.com$', 'https://test.com/', @settings)",
            new { settings = SettingsJson(0) });

        agency = new TestAgency { DatabaseFactory = new SharedDatabaseFactory(connection_string) };
        agency.LoadFromDatabase(database);
    }

    public void Dispose()
    {
        database.Dispose();
    }

    [Fact]
    public void Parallel_analyze_calls_advance_exactly_one_step_each()
    {
        var failures = new ConcurrentQueue<Exception>();

        Parallel.For(0, 50, _ =>
        {
            try
            {
                agency.AnalyzeContent("https://test.com/jobs", "<html></html>");
            }
            catch (Exception ex)
            {
                failures.Enqueue(ex);
            }
        });

        Assert.Empty(failures);
        Assert.Equal(0, agency.CurrentMethodIndex);
        Assert.False(agency.IsActiveSeeking);
        Assert.Equal(agency.CurrentMethodIndex, SavedRunning());
    }

    [Fact]
    public void ApplyRunning_linearizes_with_parallel_analyze_calls()
    {
        agency.ApplyRunning(2, database);

        Assert.Equal(2, agency.CurrentMethodIndex);
        Assert.True(agency.IsActiveSeeking);

        Parallel.For(0, 5, _ => agency.AnalyzeContent("https://test.com/jobs", "<html></html>"));

        Assert.Equal(2, agency.CurrentMethodIndex);
        Assert.False(agency.IsActiveSeeking);
        Assert.Equal(agency.CurrentMethodIndex, SavedRunning());
    }

    private static string SettingsJson(int running)
    {
        return $@"{{
  ""running"": {running},
  ""methods"": [
    {{ ""Title"": ""M0"", ""Url"": ""0"" }},
    {{ ""Title"": ""M1"", ""Url"": ""1"" }},
    {{ ""Title"": ""M2"", ""Url"": ""2"" }},
    {{ ""Title"": ""M3"", ""Url"": ""3"" }},
    {{ ""Title"": ""M4"", ""Url"": ""4"" }}
  ]
}}";
    }

    private int SavedRunning()
    {
        var settings = database.ExecuteScalar<string?>(
            "SELECT Settings FROM Agency WHERE AgencyID = @id", new { id = 1L });

        return JsonConvert.DeserializeObject<Agency.AgencySetting>(settings!)!.Running;
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
}
