using System.Data.SQLite;
using System.Text.RegularExpressions;
using Photon.JobSeeker.Analyze.Pages;
using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Tests;

internal sealed class BrokenAgency : Agency
{
    public string? JobTitle { get; set; }

    public string JobHtml { get; set; } = string.Empty;

    public override string Name => "BrokenAgency";

    public override Regex? JobAcceptabilityChecker => null;

    public override int DefaultWaiting => 2500;

    public override int DefaultPacing => 2500;

    public override string SearchLink => "https://broken.example.com/jobs";

    protected override void RunningSearchingMethodChanged(int value) { }

    protected override IEnumerable<Type> GetSubPages()
    {
        yield return typeof(BrokenJobPage);
    }
}

internal sealed class BrokenJobPage(Agency parent) : JobPage(parent)
{
    private BrokenAgency Agency => (BrokenAgency)Parent;

    protected override bool CheckInvalidUrl(string url, string content) => false;

    protected override string GetJobCode(string url) => "j1";

    protected override Command[]? JobFallow(string content) => null;

    protected override void GetJobContent(string html, out string? code, out string? apply, out string? title)
    {
        code = null;
        apply = null;
        title = Agency.JobTitle;
    }

    public override string GetHtmlContent(string html) => Agency.JobHtml;
}

internal sealed class BrokenPageFixture : IDisposable
{
    private readonly SQLiteConnection keeper;

    private readonly SQLiteConnection dictionariesKeeper;

    public BrokenPageFixture()
    {
        var connection_string = $"Data Source=file:broken{Guid.NewGuid():N}?mode=memory&cache=shared;Pooling=False";

        keeper = new SQLiteConnection(connection_string);
        keeper.Open();

        Database = new Database(keeper);
        Database.Execute(DDL_AGENCY);
        Database.Execute(DDL_JOB);
        Database.Execute(DDL_JOB_OPTION);
        Database.Execute(DDL_APP_SETTING);
        Database.Execute(@"
INSERT INTO Agency (AgencyID, Title, Active, Domain, Link, Settings)
VALUES (1, 'BrokenAgency', 3, 'broken\.example\.com$', 'https://broken.example.com/', @settings)",
            new { settings = SettingsJson() });
        Database.Execute(@"
INSERT INTO JobOption (Efective, Category, Score, Title, Pattern)
VALUES (1, 'field', 100, 'Backend', 'backend')");
        Database.Execute(@"
INSERT INTO Job (JobID, AgencyID, Country, Code, State, Url, Title, Html, Content, Attempts)
VALUES (1, 1, 'M0', 'j1', 'Saved', 'https://broken.example.com/jobs/j1',
'Old Title', '<html>old</html>', 'old content', 2)");

        var dictionaries_path = $"file:dict{Guid.NewGuid():N}?mode=memory&cache=shared";

        dictionariesKeeper = new SQLiteConnection($"Data Source={dictionaries_path};Pooling=False");
        dictionariesKeeper.Open();
        using (var command = dictionariesKeeper.CreateCommand())
        {
            command.CommandText = "CREATE TABLE en_US (Word TEXT)";
            command.ExecuteNonQuery();
            command.CommandText = "INSERT INTO en_US (Word) VALUES ('backend'), ('alpha'), ('beta')";
            command.ExecuteNonQuery();
        }

        Dictionaries.SetConfiguration(path: dictionaries_path);
        JobEligibilityHelper.InvalidateOptionsCache();

        Analyzer = new Analyzer(new SharedDatabaseFactory(connection_string));
        _ = Analyzer.Agencies;
    }

    public Database Database { get; }

    public Analyzer Analyzer { get; }

    public BrokenAgency Agency => (BrokenAgency)Analyzer.Agencies["BrokenAgency"];

    public Page JobPage => Agency.Pages.Single(p => p is JobPage);

    public void Dispose()
    {
        Database.Dispose();
        dictionariesKeeper.Dispose();
    }

    private static string SettingsJson()
    {
        return @"{ ""running"": 0, ""methods"": [ { ""Title"": ""M0"", ""Url"": ""0"" } ] }";
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

    private const string DDL_JOB_OPTION = @"
create table JobOption (
    JobOptionID     integer     not null    primary key,
    Efective        bit         not null    default 1,
    Category        text        not null,
    Score           integer     not null,
    Title           text        not null,
    Pattern         text        not null,
    Settings        text            null,
    unique (Title)
)";

    private const string DDL_APP_SETTING = @"
create table AppSetting (
    Key     text    not null    primary key,
    Value   text    not null
)";
}

public class BrokenJobPageTests
{
    private const string JobUrl = "https://broken.example.com/jobs/j1";

    [Fact]
    public void Missing_title_throws_BadJobRequest_and_preserves_row()
    {
        using var fixture = new BrokenPageFixture();
        fixture.Agency.JobTitle = null;
        fixture.Agency.JobHtml = "<html><body>backend alpha beta</body></html>";

        var exception = Assert.Throws<BadJobRequest>(
            () => fixture.JobPage.IssueCommand(JobUrl, "<html>garbage</html>"));

        Assert.Contains("title missing", exception.Message);
        AssertBrokenRowUntouched(fixture);
    }

    [Fact]
    public void Empty_text_content_throws_BadJobRequest_and_preserves_row()
    {
        using var fixture = new BrokenPageFixture();
        fixture.Agency.JobTitle = "Backend Developer";
        fixture.Agency.JobHtml = "<html><body></body></html>";

        var exception = Assert.Throws<BadJobRequest>(
            () => fixture.JobPage.IssueCommand(JobUrl, "<html>garbage</html>"));

        Assert.Contains("content missing", exception.Message);
        AssertBrokenRowUntouched(fixture);
    }

    [Fact]
    public void Good_page_updates_content_and_moves_state_off_saved()
    {
        using var fixture = new BrokenPageFixture();
        fixture.Agency.JobTitle = "Backend Developer";
        fixture.Agency.JobHtml = "<html><body>backend alpha beta</body></html>";

        var commands = fixture.JobPage.IssueCommand(JobUrl, "<html>valid</html>");

        Assert.NotNull(commands);
        Assert.Equal("AiPending", fixture.Database.ExecuteScalar<string>("SELECT State FROM Job"));
        Assert.Equal("Backend Developer", fixture.Database.ExecuteScalar<string>("SELECT Title FROM Job"));
        Assert.Equal("<html><body>backend alpha beta</body></html>",
            fixture.Database.ExecuteScalar<string>("SELECT Html FROM Job"));
        Assert.Equal(" backend alpha beta",
            fixture.Database.ExecuteScalar<string>("SELECT Content FROM Job"));
    }

    [Fact]
    public void UpdateScrapedJob_skips_empty_html_and_content()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("bg1", "https://example.com/jobs/bg1");
        db.ExecuteRaw(
            @"UPDATE Job SET Html = '<html>keep</html>', Content = 'keep text' WHERE Code = 'bg1'");
        var job = db.Database.Job.Fetch(GoldenDatabase.AgencyId, "bg1")!;
        job.Html = null;
        job.Content = "";

        db.Database.Job.UpdateScrapedJob(job, codeChanged: false, linkFound: false, includeState: false);

        Assert.Equal("<html>keep</html>", db.Scalar("SELECT Html FROM Job"));
        Assert.Equal("keep text", db.Scalar("SELECT Content FROM Job"));
    }

    [Fact]
    public void UpdateScrapedJob_overwrites_when_html_and_content_nonempty()
    {
        using var db = new GoldenDatabase();
        db.SaveSearchJob("bg2", "https://example.com/jobs/bg2");
        db.ExecuteRaw(
            @"UPDATE Job SET Html = '<html>old</html>', Content = 'old text' WHERE Code = 'bg2'");
        var job = db.Database.Job.Fetch(GoldenDatabase.AgencyId, "bg2")!;
        job.Html = "<html>new</html>";
        job.Content = "new text";

        db.Database.Job.UpdateScrapedJob(job, codeChanged: false, linkFound: false, includeState: false);

        Assert.Equal("<html>new</html>", db.Scalar("SELECT Html FROM Job"));
        Assert.Equal("new text", db.Scalar("SELECT Content FROM Job"));
    }

    private static void AssertBrokenRowUntouched(BrokenPageFixture fixture)
    {
        Assert.Equal("Saved", fixture.Database.ExecuteScalar<string>("SELECT State FROM Job"));
        Assert.Equal("Old Title", fixture.Database.ExecuteScalar<string>("SELECT Title FROM Job"));
        Assert.Equal("<html>old</html>", fixture.Database.ExecuteScalar<string>("SELECT Html FROM Job"));
        Assert.Equal("old content", fixture.Database.ExecuteScalar<string>("SELECT Content FROM Job"));
        Assert.Equal(2L, fixture.Database.ExecuteScalar<long>("SELECT Attempts FROM Job"));
    }
}
