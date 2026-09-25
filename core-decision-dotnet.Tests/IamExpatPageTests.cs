using System.Data.SQLite;
using Photon.JobSeeker.Pages;
using IamExpatAgency = Photon.JobSeeker.IamExpat.IamExpat;
using IamExpatJobPage = Photon.JobSeeker.IamExpat.IamExpatPageJob;

namespace Photon.JobSeeker.Tests;

public class IamExpatPageTests : IDisposable
{
    private readonly SQLiteConnection keeper;
    private readonly Database database;
    private readonly IamExpatAgency agency;

    public IamExpatPageTests()
    {
        var connection_string = $"Data Source=file:iamexpat{Guid.NewGuid():N}?mode=memory&cache=shared;Pooling=False";

        keeper = new SQLiteConnection(connection_string);
        keeper.Open();

        database = new Database(keeper);
        database.Execute(DDL_AGENCY);
        database.Execute(DDL_JOB);
        database.Execute(DDL_TREND);
        database.Execute(@"
INSERT INTO Agency (AgencyID, Title, Active, Domain, Link, Settings)
VALUES (1, 'IamExpat', 1, '(.+\.)?iamexpat\.(nl|de|ch|com)$', 'https://www.iamexpat.nl', @settings)",
            new { settings = SettingsJson() });

        agency = new IamExpatAgency { DatabaseFactory = new SharedDatabaseFactory(connection_string) };
        agency.LoadFromDatabase(database);
    }

    public void Dispose()
    {
        database.Dispose();
    }

    [Fact]
    public void Unfiltered_search_page_redirects_to_the_it_technology_category()
    {
        var result = agency.AnalyzeContent("https://www.iamexpat.nl/career/jobs-netherlands", "<html></html>");

        Assert.Equal(TrendState.Seeking, result.State);
        var command = Assert.Single(result.Commands);
        Assert.Equal("go", command.Action);
        Assert.Equal("https://www.iamexpat.nl/career/jobs-netherlands/it-technology-positions", command.Params!["url"]);
    }

    [Fact]
    public void Other_category_page_redirects_to_the_it_technology_category()
    {
        var result = agency.AnalyzeContent("https://www.iamexpat.nl/career/jobs-netherlands/engineering-positions", "<html></html>");

        var command = Assert.Single(result.Commands);
        Assert.Equal("go", command.Action);
        Assert.Equal("https://www.iamexpat.nl/career/jobs-netherlands/it-technology-positions", command.Params!["url"]);
    }

    [Fact]
    public void Category_page_extracts_jobs_and_opens_the_next_page()
    {
        var result = agency.AnalyzeContent(
            "https://www.iamexpat.nl/career/jobs-netherlands/it-technology-positions", CategoryPageSnippet);

        var command = Assert.Single(result.Commands);
        Assert.Equal("go", command.Action);
        Assert.Equal("https://www.iamexpat.nl/career/jobs-netherlands/it-technology-positions?page=2", command.Params!["url"]);

        Assert.Equal(2L, database.ExecuteScalar<long>("SELECT COUNT(*) FROM Job"));
        Assert.Equal(
            "https://www.iamexpat.nl/career/jobs-netherlands/it-technology-positions/data-center-technician-english/hd16LSmhBpsXuHN5sfQcrP",
            database.ExecuteScalar<string>("SELECT Url FROM Job WHERE Code = 'hd16LSmhBpsXuHN5sfQcrP'"));
        Assert.Equal(nameof(JobState.Saved),
            database.ExecuteScalar<string>("SELECT State FROM Job WHERE Code = 'hd16LSmhBpsXuHN5sfQcrP'"));
        Assert.Equal(1L,
            database.ExecuteScalar<long>("SELECT COUNT(*) FROM Job WHERE Code = 'oZMM5LUYhm7Prk6A2q5qMd'"));
    }

    [Fact]
    public void Next_page_number_follows_the_page_url_parameter()
    {
        var result = agency.AnalyzeContent(
            "https://www.iamexpat.nl/career/jobs-netherlands/it-technology-positions?page=3", CategoryPageSnippet);

        var command = Assert.Single(result.Commands);
        Assert.Equal("https://www.iamexpat.nl/career/jobs-netherlands/it-technology-positions?page=4", command.Params!["url"]);
    }

    [Fact]
    public void Empty_page_after_the_last_one_finishes_the_search()
    {
        var result = agency.AnalyzeContent(
            "https://www.iamexpat.nl/career/jobs-netherlands/it-technology-positions?page=2",
            """<html><body><h1 class="title-5">IT &amp; technology jobs in the Netherlands</h1></body></html>""");

        Assert.Empty(result.Commands);
        Assert.Equal(TrendState.Finished, result.State);
    }

    [Fact]
    public void Job_page_content_is_sliced_between_markers()
    {
        var job_page = new IamExpatJobPage(agency);

        var html = job_page.GetHtmlContent(JobPageSnippet);

        Assert.Contains("About this role", html);
        Assert.Contains("Are you an", html);
        Assert.Contains("IT professional", html);
        Assert.DoesNotContain("Similar jobs", html);
        Assert.DoesNotContain("LATEST CAREER NEWS", html);
    }

    [Fact]
    public void Job_page_extracts_code_apply_link_and_title()
    {
        var job_page = new ExposedIamExpatJobPage(agency);

        Assert.Equal("crMYMyCSPDKYg2BhLT2Ctm", job_page.Code(JobPageUrl));

        job_page.Content(JobPageSnippet, out var code, out var apply, out var title);

        Assert.Null(code);
        Assert.Equal("https://undutchables.nl/vacancies/it-support-technician", apply);
        Assert.Equal("IT Support Technician", title);
    }

    [Fact]
    public void Job_page_title_ignores_the_jsonld_description_h1()
    {
        var job_page = new ExposedIamExpatJobPage(agency);

        job_page.Content(EmployerSloganJobPageSnippet, out _, out _, out var title);

        Assert.Equal("Development Architect (f/m/d) Utilities Industry - German Energy Market Communication", title);
    }

    [Fact]
    public void Job_page_title_falls_back_to_the_document_title()
    {
        var job_page = new ExposedIamExpatJobPage(agency);

        job_page.Content(DocumentTitleSnippet, out _, out _, out var title);

        Assert.Equal("Automation Engineer (temp)", title);
    }

    [Fact]
    public void Job_page_title_fallback_ignores_any_plain_h1()
    {
        var job_page = new ExposedIamExpatJobPage(agency);

        job_page.Content(SloganWithoutTitleClassSnippet, out _, out _, out var title);

        Assert.Equal("Working Student (f/m/d) - IT Portfolio", title);
    }

    private sealed class ExposedIamExpatJobPage(IamExpatAgency parent) : IamExpatJobPage(parent)
    {
        public string Code(string url) => GetJobCode(url);

        public void Content(string html, out string? code, out string? apply, out string? title)
            => GetJobContent(html, out code, out apply, out title);
    }

    private const string JobPageUrl =
        "https://www.iamexpat.nl/career/jobs-netherlands/it-technology-positions/it-support-technician/crMYMyCSPDKYg2BhLT2Ctm";

    private const string EmployerSloganJobPageSnippet =
        """
        <html><head>
        <script type="application/ld+json">{"@context":"https://schema.org","@type":"JobPosting","title":"Development Architect (f/m/d) Utilities Industry - German Energy Market Communication","description":"<div><h1>We help the world run better</h1><p>SAP</p></div>"}</script>
        <title>Development Architect (f/m/d) Utilities Industry - German Energy Market Communication</title>
        </head><body>
        <div class="BodyTop_wrapper__MP_2N"><div class="BodyTop_main__EAqmj"><div class="flex flex-col gap-2"><h1 class="title-3">Development Architect (f/m/d) Utilities Industry - German Energy Market Communication</h1></div></div></div>
        <div class="BodyCenter_main__Sz_2E"><main class="MainContent_styles_mainContent__cQTb5"><div>Job description body.</div></main></div>
        </body></html>
        """;

    private const string DocumentTitleSnippet =
        """
        <html><head><title>Automation Engineer (temp)</title></head>
        <body><div class="BodyCenter_main__Sz_2E"><main class="MainContent_styles_mainContent__cQTb5"><div>Job description body.</div></main></div></body></html>
        """;

    private const string SloganWithoutTitleClassSnippet =
        """
        <html><head>
        <script type="application/ld+json">{"@context":"https://schema.org","@type":"JobPosting","description":"<div><h1>We help the world run better</h1></div>"}</script>
        <title>Working Student (f/m/d) - IT Portfolio</title>
        </head><body>
        <div class="BodyTop_main__EAqmj"><div class="flex flex-col gap-2"><h1>We help the world run better</h1></div></div>
        </body></html>
        """;

    private const string CategoryPageSnippet =
        """
        <html><body>
        <a href="/career/jobs-netherlands/it-technology-positions/data-center-technician-english/hd16LSmhBpsXuHN5sfQcrP" class="JobBoardItemCard_cardWrapper__zmd_i" target="_blank" rel="noopener">Data Center Technician (English)</a>
        <a href="/career/jobs-netherlands/it-technology-positions/data-center-technician-english/hd16LSmhBpsXuHN5sfQcrP" class="JobBoardItemCard_cardWrapper__zmd_i" target="_blank" rel="noopener">Data Center Technician (English)</a>
        <a href="/career/jobs-netherlands/it-technology-positions/sap-o2c-consultant-temporaryfreelance/oZMM5LUYhm7Prk6A2q5qMd" class="JobBoardItemCard_cardWrapper__zmd_i" target="_blank" rel="noopener">SAP O2C Consultant (temporary/freelance)</a>
        <a href="/career/jobs-netherlands/engineering-positions/contract-packaging-manager-temp/9RdYgz5k2MGzVGcX2pp4A9" class="JobBoardItemCard_cardWrapper__zmd_i" target="_blank" rel="noopener">Contract Packaging Manager</a>
        </body></html>
        """;

    private const string JobPageSnippet =
        """
        <html><body>
        <header><a href="/auth/login" class="body-medium rounded-md bg-black text-white" target="_blank" rel="noopener">Apply for this position</a></header>
        <h1 class="title-3">IT Support Technician</h1>
        <div class="BodyTop_wrapperLogo__jhd_p"><label class="flex items-center gap-x-2.5"><span class="title-9 hidden desktop:block">Bookmark</span><button class="border rounded-md"><svg></svg></button></label></div>
        <div class="BodyCenter_main__Sz_2E"><div class="relative pt-10 flex flex-col gap-[0.375rem] sm:pt-0"><h2 class="headline-2">About this role</h2><main class="MainContent_styles_mainContent__cQTb5 pl-5"><div>Are you an <strong class="body-large">IT professional</strong> who enjoys solving technical problems? <a href="https://undutchables.nl/vacancies/it-support-technician" class="body-medium rounded-md bg-black text-white" target="_blank" rel="noopener">Apply for this position</a></div></main></div></div>
        <h2 class="headline-2">Similar jobs</h2>
        <h2 class="title-1 uppercase m-0">LATEST CAREER NEWS &amp; ARTICLES</h2>
        </body></html>
        """;

    private static string SettingsJson()
    {
        return @"{ ""running"": 0, ""methods"": [
  { ""Title"": ""NL"", ""Url"": ""nl/career/jobs-netherlands"" } ] }";
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
    Challenge       bit         not null    default 0,
    unique (AgencyID, Type)
)";
}
