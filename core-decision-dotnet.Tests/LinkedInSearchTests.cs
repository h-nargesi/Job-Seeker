using System.Data.SQLite;
using LinkedInAgency = Photon.JobSeeker.LinkedIn.LinkedIn;

namespace Photon.JobSeeker.Tests;

[Collection("LinkedIn")]
public class LinkedInSearchTests
{
    private const string SearchUrl =
        "https://www.linkedin.com/jobs/search/?keywords=developer&refresh=true&f_AL=true&f_WT=2&f_E=3%2C4&location=Netherlands";

    [Fact]
    public void Empty_jobs_home_redirects_to_the_keyword_search_url()
    {
        using var fixture = Fixture.Create();

        var result = fixture.Agency.AnalyzeContent(
            "https://www.linkedin.com/jobs/search/?geoId=92000000&keywords=&location=Worldwide&sortBy=DD",
            "<html></html>");

        Assert.Equal(TrendState.Seeking, result.State);
        var command = Assert.Single(result.Commands);
        Assert.Equal("go", command.Action);
        Assert.Equal(
            "/jobs/search/?keywords=developer&refresh=true&f_AL=true&f_WT=2&f_E=3%2C4&location=Netherlands",
            command.Params!["url"]);
    }

    [Fact]
    public void Keyword_search_url_is_accepted_and_does_not_redirect_to_itself()
    {
        using var fixture = Fixture.Create();

        var result = fixture.Agency.AnalyzeContent(SearchUrl, "<html></html>");

        Assert.Null(GetGoUrl(result));
    }

    [Fact]
    public void GeoId_only_method_matches_its_own_search_url()
    {
        using var fixture = Fixture.Create("&f_AL=true&f_WT=2&f_E=3%2C4&geoId=91000002");

        var geo_url =
            "https://www.linkedin.com/jobs/search/?keywords=developer&refresh=true&f_AL=true&f_WT=2&f_E=3%2C4&geoId=91000002";

        var result = fixture.Agency.AnalyzeContent(geo_url, "<html></html>");

        Assert.Null(GetGoUrl(result));
    }

    private static string? GetGoUrl(Result result)
    {
        var go = result.Commands?.FirstOrDefault(c => c.Action == "go");
        return go?.Params?["url"] as string;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly SQLiteConnection keeper;

        public Database Database { get; }

        public LinkedInAgency Agency { get; }

        private Fixture(SQLiteConnection keeper, Database database, LinkedInAgency agency)
        {
            this.keeper = keeper;
            Database = database;
            Agency = agency;
        }

        public static Fixture Create(string method_url = "&f_AL=true&f_WT=2&f_E=3%2C4&location=Netherlands")
        {
            var connection_string = $"Data Source=file:linkedin{Guid.NewGuid():N}?mode=memory&cache=shared;Pooling=False";

            var keeper = new SQLiteConnection(connection_string);
            keeper.Open();

            var database = new Database(keeper);
            database.Execute(DDL_AGENCY);
            database.Execute(DDL_JOB);
            database.Execute(DDL_TREND);
            database.Execute(@"
INSERT INTO Agency (AgencyID, Title, Active, Domain, Link, Settings)
VALUES (1, 'LinkedIn', 3, '(.+\.)?linkedin\.com$', 'https://linkedin.com', @settings)",
                new { settings = SettingsJson(method_url) });

            var agency = new LinkedInAgency { DatabaseFactory = new SharedDatabaseFactory(connection_string) };
            agency.LoadFromDatabase(database);

            return new Fixture(keeper, database, agency);
        }

        public void Dispose()
        {
            Database.Dispose();
            keeper.Dispose();
        }

        private static string SettingsJson(string method_url)
        {
            return @$"{{ ""running"": 0, ""methods"": [
  {{ ""Title"": ""NL"", ""Url"": ""{method_url}"" }} ] }}";
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
    ResumeText          text            null,
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
}
