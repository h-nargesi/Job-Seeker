using System.Data.SQLite;

namespace Photon.JobSeeker.Tests;

public class AgencyRegionTests : IDisposable
{
    private readonly SQLiteConnection keeper;

    public AgencyRegionTests()
    {
        var connection_string = $"Data Source=file:regions{Guid.NewGuid():N}?mode=memory&cache=shared;Pooling=False";

        keeper = new SQLiteConnection(connection_string);
        keeper.Open();

        var database = new Database(keeper);
        database.Execute(DDL_AGENCY);

        var analyzer = new Analyzer(new SharedDatabaseFactory(connection_string));
        indeed = (Indeed.Indeed)analyzer.Agencies["Indeed"];
        linkedin = (LinkedIn.LinkedIn)analyzer.Agencies["LinkedIn"];
        iamexpat = (IamExpat.IamExpat)analyzer.Agencies["IamExpat"];
        bayt = (Bayt.Bayt)analyzer.Agencies["Bayt"];
        stepstone = (Stepstone.Stepstone)analyzer.Agencies["Stepstone"];
    }

    private readonly Indeed.Indeed indeed;
    private readonly LinkedIn.LinkedIn linkedin;
    private readonly IamExpat.IamExpat iamexpat;
    private readonly Bayt.Bayt bayt;
    private readonly Stepstone.Stepstone stepstone;

    public void Dispose()
    {
        keeper.Dispose();
    }

    [Theory]
    [InlineData("https://nl.indeed.com/jobs?q=developer", "NL")]
    [InlineData("https://fi.indeed.com/viewjob?jk=0123456789abcdef", "FI")]
    [InlineData("https://om.indeed.com/jobs?q=developer", "OM")]
    [InlineData("https://www.indeed.com/jobs?q=developer", "")]
    [InlineData("https://dk.indeed.com/jobs?q=developer", "")]
    [InlineData("not a url", "")]
    public void Indeed_resolves_region_from_host(string url, string expected)
    {
        Assert.Equal(expected, indeed.ParseRegion(url).Title);
    }

    [Theory]
    [InlineData("https://www.linkedin.com/jobs/search/?keywords=developer&refresh=true&f_WT=2&f_E=3%2C4&location=Germany", "DE")]
    [InlineData("https://www.linkedin.com/jobs/search/?keywords=developer&location=Netherlands", "NL")]
    [InlineData("https://www.linkedin.com/jobs/search/?keywords=developer&geoId=91000002", "EU")]
    [InlineData("https://www.linkedin.com/jobs/search/?keywords=developer&location=United%20Kingdom", "UK")]
    [InlineData("https://www.linkedin.com/jobs/search/?keywords=developer&location=France", "")]
    [InlineData("https://www.linkedin.com/jobs/view/12345/", "")]
    public void LinkedIn_resolves_region_from_location_query(string url, string expected)
    {
        Assert.Equal(expected, linkedin.ParseRegion(url).Title);
    }

    [Theory]
    [InlineData("https://www.iamexpat.nl/career/jobs-netherlands/it-technology-positions/testing/some-slug-abc12345", "NL")]
    [InlineData("https://www.iamexpat.nl/career/jobs-netherlands?page=2", "NL")]
    [InlineData("https://www.iamexpat.ch/career/jobs-switzerland/it-technology-positions/testing/some-slug-abc12345", "CH")]
    [InlineData("https://www.iamexpat.de/career/jobs-netherlands", "DE")]
    [InlineData("https://www.iamexpat.com/career/jobs-netherlands", "")]
    public void IamExpat_resolves_region_from_host(string url, string expected)
    {
        Assert.Equal(expected, iamexpat.ParseRegion(url).Title);
    }

    [Theory]
    [InlineData("https://www.bayt.com/en/oman/jobs/developer-jobs/", "OM")]
    [InlineData("https://www.bayt.com/en/qatar/jobs/developer-jobs/?page=2", "QA")]
    [InlineData("https://www.bayt.com/en/oman/jobs/senior-developer-1234/", "OM")]
    [InlineData("https://www.bayt.com/en/uae/jobs/developer-jobs/", "")]
    public void Bayt_resolves_region_from_path(string url, string expected)
    {
        Assert.Equal(expected, bayt.ParseRegion(url).Title);
    }

    [Fact]
    public void Stepstone_resolves_its_single_region()
    {
        Assert.Equal("DE", stepstone.ParseRegion("https://www.stepstone.de/work/full-time/developer").Title);
    }

    [Theory]
    [InlineData("https://nl.indeed.com/jobs?q=developer", "https://nl.indeed.com")]
    [InlineData("https://www.bayt.com/en/oman/jobs/developer-jobs/?page=3", "https://www.bayt.com")]
    [InlineData("https://www.linkedin.com/jobs/search/?keywords=x", "https://www.linkedin.com")]
    public void GetBaseUrl_keeps_scheme_and_host_only(string url, string expected)
    {
        Assert.Equal(expected, Pages.PageUtils.GetBaseUrl(url));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    public void GetBaseUrl_returns_input_when_not_absolute(string url)
    {
        Assert.Equal(url, Pages.PageUtils.GetBaseUrl(url));
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
);

insert into Agency (Title, Domain, Link, Settings) values
('Indeed', '(.+\.)?indeed\.com$', 'https://www.indeed.com',
 '{ ""running"": 0, ""methods"": [
   { ""Title"": ""NL"", ""Url"": ""https://nl.indeed.com/"" },
   { ""Title"": ""AU"", ""Url"": ""https://au.indeed.com/"" },
   { ""Title"": ""DE"", ""Url"": ""https://de.indeed.com/"" },
   { ""Title"": ""OM"", ""Url"": ""https://om.indeed.com/"", ""Enabled"": false },
   { ""Title"": ""FI"", ""Url"": ""https://fi.indeed.com/"" }] }'),

('LinkedIn', '(.+\.)?linkedin\.com$', 'https://www.linkedin.com',
 '{ ""running"": 0, ""methods"": [
   { ""Title"": ""NL"", ""Url"": ""&f_WT=2&f_E=3%2C4&location=Netherlands"" },
   { ""Title"": ""DE"", ""Url"": ""&f_WT=2&f_E=3%2C4&location=Germany"" },
   { ""Title"": ""EU"", ""Url"": ""&f_WT=2&f_E=3%2C4&geoId=91000002"" },
   { ""Title"": ""UK"", ""Url"": ""&f_WT=2&f_E=3%2C4&location=United Kingdom"" }] }'),

('IamExpat', '(.+\.)?iamexpat\.(nl|de|ch|com)$', 'https://www.iamexpat.com',
 '{ ""running"": 0, ""methods"": [
   { ""Title"": ""NL"", ""Url"": ""nl/career/jobs-netherlands"" },
   { ""Title"": ""DE"", ""Url"": ""de/career/jobs-germany"" },
   { ""Title"": ""CH"", ""Url"": ""ch/career/jobs-switzerland"" }] }'),

('Bayt', '(.+\.)?bayt\.com$', 'https://www.bayt.com',
 '{ ""running"": 0, ""methods"": [
   { ""Title"": ""OM"", ""Url"": ""oman"" },
   { ""Title"": ""QA"", ""Url"": ""qatar"" }] }'),

('Stepstone', '(.+\.)?stepstone\.de$', 'https://stepstone.de',
 '{ ""running"": 0, ""methods"": [
   { ""Title"": ""DE"", ""Url"": """" }] }');
";

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
