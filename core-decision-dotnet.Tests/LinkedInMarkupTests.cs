using System.Data.SQLite;
using Photon.JobSeeker.Pages;
using LinkedInAgency = Photon.JobSeeker.LinkedIn.LinkedIn;
using LinkedInJobPage = Photon.JobSeeker.LinkedIn.LinkedInPageJob;

namespace Photon.JobSeeker.Tests;

[Collection("LinkedIn")]
public class LinkedInMarkupTests : IDisposable
{
    private const string SearchUrl =
        "https://www.linkedin.com/jobs/search/?keywords=developer&refresh=true&f_AL=true&f_WT=2&f_E=3%2C4&location=Netherlands";

    private const string JobUrl = "https://www.linkedin.com/jobs/view/4463010562/";

    private readonly SQLiteConnection keeper;
    private readonly Database database;
    private readonly LinkedInAgency agency;

    public LinkedInMarkupTests()
    {
        var connection_string = $"Data Source=file:linkedinmarkup{Guid.NewGuid():N}?mode=memory&cache=shared;Pooling=False";

        keeper = new SQLiteConnection(connection_string);
        keeper.Open();

        database = new Database(keeper);
        database.Execute(DDL_AGENCY);
        database.Execute(DDL_JOB);
        database.Execute(DDL_TREND);
        database.Execute(@"
INSERT INTO Agency (AgencyID, Title, Active, Domain, Link, Settings)
VALUES (1, 'LinkedIn', 3, '(.+\.)?linkedin\.com$', 'https://linkedin.com', @settings)",
            new { settings = SettingsJson() });

        agency = new LinkedInAgency { DatabaseFactory = new SharedDatabaseFactory(connection_string) };
        agency.LoadFromDatabase(database);
    }

    public void Dispose()
    {
        database.Dispose();
        keeper.Dispose();
    }

    [Fact]
    public void Search_results_example_extracts_all_job_urls()
    {
        var result = agency.AnalyzeContent(SearchUrl, ReadExample("linkedin.search.html"));

        Assert.Equal(TrendState.Seeking, result.State);
        Assert.Empty(result.Commands.Where(c => c.Action == "go"));
        Assert.Equal(@"button[aria-label=""Page 8""]",
            Assert.Single(result.Commands.Where(c => c.Action == "click")).Object);
        Assert.Contains(result.Commands, c => c.Action == "recheck");
        Assert.Equal(12L, database.ExecuteScalar<long>("SELECT COUNT(*) FROM Job"));
        Assert.Equal("https://linkedin.com/jobs/view/4469749917/",
            database.ExecuteScalar<string>("SELECT Url FROM Job WHERE Code = '4469749917'"));
    }

    [Fact]
    public void New_pagination_markup_opens_the_next_page()
    {
        var result = agency.AnalyzeContent(SearchUrl, MultiPageSnippet);

        var click = Assert.Single(result.Commands.Where(c => c.Action == "click"));
        Assert.Equal(@"button[aria-label=""Page 2""]", click.Object);
        Assert.Contains(result.Commands, c => c.Action == "recheck");
    }

    [Fact]
    public void Job_example_title_is_taken_from_the_top_card_paragraph()
    {
        var job_page = new ExposedLinkedInJobPage(agency);

        job_page.Content(ReadExample("linkedin.details.html"), out var code, out _, out var title);

        Assert.Null(code);
        Assert.Equal("Javascript Developer - Remote", title);
    }

    [Fact]
    public void Job_title_falls_back_to_the_document_title_without_company()
    {
        var job_page = new ExposedLinkedInJobPage(agency);

        job_page.Content(DocumentTitleSnippet, out _, out _, out var title);

        Assert.Equal("Senior Backend Engineer", title);
    }

    [Fact]
    public void Job_example_content_is_taken_from_the_expandable_text_box()
    {
        var job_page = new ExposedLinkedInJobPage(agency);

        var html = job_page.Html(ReadExample("linkedin.details.html"));

        Assert.Contains("Javascript Developer - Remote", html);
        Assert.Contains("train next-generation AI systems", html);
        Assert.DoesNotContain("Set alert for similar jobs", html);
        Assert.DoesNotContain("YO IT Consulting", html);
    }

    private static string ReadExample(string name)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", name));
    }

    private sealed class ExposedLinkedInJobPage(LinkedInAgency parent) : LinkedInJobPage(parent)
    {
        public string Html(string html) => GetHtmlContent(html);

        public void Content(string html, out string? code, out string? apply, out string? title)
            => GetJobContent(html, out code, out apply, out title);
    }

    private const string DocumentTitleSnippet = """
        <html><head><title>Senior Backend Engineer | ACME Corp | LinkedIn</title></head>
        <body></body></html>
        """;

    private const string MultiPageSnippet = """
        <html><body>
        <a href="/jobs/view/1111111111/?trk=x">Job One</a>
        <a href="/jobs/view/2222222222/?trk=x">Job Two</a>
        <ul class="jobs-search-pagination__pages">
        <li class="active"><button class="jobs-search-pagination__indicator-button jobs-search-pagination__indicator-button--active" aria-current="page" aria-label="Page 1" type="button">1</button></li>
        <li><button class="jobs-search-pagination__indicator-button" aria-label="Page 2" type="button">2</button></li>
        </ul>
        </body></html>
        """;

    private static string SettingsJson()
    {
        return @"{ ""running"": 0, ""methods"": [
  { ""Title"": ""NL"", ""Url"": ""&f_AL=true&f_WT=2&f_E=3%2C4&location=Netherlands"" } ] }";
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
