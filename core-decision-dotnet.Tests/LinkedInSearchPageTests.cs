using System.Data.SQLite;
using LinkedInAgency = Photon.JobSeeker.LinkedIn.LinkedIn;

namespace Photon.JobSeeker.Tests;

public class LinkedInSearchPageTests : IDisposable
{
    private readonly SQLiteConnection keeper;
    private readonly Database database;
    private readonly LinkedInAgency agency;

    public LinkedInSearchPageTests()
    {
        var connection_string = $"Data Source=file:linkedinsearch{Guid.NewGuid():N}?mode=memory&cache=shared;Pooling=False";

        keeper = new SQLiteConnection(connection_string);
        keeper.Open();

        database = new Database(keeper);
        database.Execute(DDL_AGENCY);
        database.Execute(DDL_JOB);
        database.Execute(DDL_TREND);
        database.Execute(@"
INSERT INTO Agency (AgencyID, Title, Active, Domain, Link, Settings)
VALUES (1, 'LinkedIn', 3, '(.+\.)?linkedin\.com$', 'https://www.linkedin.com', @settings)",
            new { settings = SettingsJson() });

        agency = new LinkedInAgency { DatabaseFactory = new SharedDatabaseFactory(connection_string) };
        agency.LoadFromDatabase(database);
    }

    public void Dispose()
    {
        database.Dispose();
    }

    [Fact]
    public void Search_page_of_the_running_region_is_accepted()
    {
        var result = agency.AnalyzeContent(RunningRegionSearchUrl, SearchResultsSnippet);

        Assert.Equal(TrendState.Seeking, result.State);
        var command = Assert.Single(result.Commands, c => c.Action == "click");
        Assert.Contains("Page 2", command.Object ?? string.Empty);
    }

    [Fact]
    public void Search_page_of_another_region_redirects_to_the_running_region()
    {
        var result = agency.AnalyzeContent(
            "https://www.linkedin.com/jobs/search/?keywords=developer&refresh=true&f_WT=2&f_E=3%2C4&location=Germany",
            "<html></html>");

        var command = Assert.Single(result.Commands);
        Assert.Equal("go", command.Action);
        Assert.Equal("/jobs/search/?keywords=developer&refresh=true&f_WT=2&f_E=3%2C4&location=Netherlands",
            command.Params!["url"]);
    }

    [Fact]
    public void Search_page_without_the_location_parameter_redirects_to_the_running_region()
    {
        var result = agency.AnalyzeContent(
            "https://www.linkedin.com/jobs/search/?keywords=developer&refresh=true&f_WT=2&f_E=3%2C4",
            "<html></html>");

        var command = Assert.Single(result.Commands);
        Assert.Equal("go", command.Action);
    }

    private const string RunningRegionSearchUrl =
        "https://www.linkedin.com/jobs/search/?keywords=developer&refresh=true&f_WT=2&f_E=3%2C4&location=Netherlands";

    private const string SearchResultsSnippet =
        """
        <html><body>
        <ul class="artdeco-pagination__pages button-list">
        <button aria-current="page" type="button">1</button>
        <button aria-label="Page 2" type="button">2</button>
        </ul>
        </body></html>
        """;

    private static string SettingsJson()
    {
        return @"{ ""running"": 0, ""methods"": [
  { ""Title"": ""NL"", ""Url"": ""&f_WT=2&f_E=3%2C4&location=Netherlands"" },
  { ""Title"": ""DE"", ""Url"": ""&f_WT=2&f_E=3%2C4&location=Germany"" } ] }";
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
    PublishedAt timestamp    null,
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
    ResumeText  text            null,
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
    Challenge       bit         not null    default 0,
    unique (AgencyID, Type)
)";
}
