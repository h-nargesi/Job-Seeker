using System.Data.SQLite;
using System.Text.RegularExpressions;
using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Tests;

internal sealed class LoginGuardAgency : Agency
{
    public const string Title = "LoginGuardAgency";

    public override string Name => Title;

    public override Regex? JobAcceptabilityChecker => null;

    public override string SearchLink => "https://guard.example.com/jobs";

    protected override void RunningSearchingMethodChanged(int value) { }

    protected override IEnumerable<Type> GetSubPages()
    {
        yield return typeof(LoginGuardPage);
    }
}

internal sealed class LoginGuardPage(Agency parent) : LoginPage(parent)
{
    public bool LoginCommandsCalled { get; private set; }

    public (string user, string pass) ReceivedCredentials { get; private set; }

    protected override bool CheckInvalidUrl(string url, string content) => false;

    protected override Command[] LoginCommands(string user, string pass)
    {
        LoginCommandsCalled = true;
        ReceivedCredentials = (user, pass);

        return
        [
            Command.Fill("#user", user),
            Command.Fill("#pass", pass),
            Command.Click("#submit")
        ];
    }
}

internal sealed class LoginGuardDatabase : IDisposable
{
    private readonly SQLiteConnection keeper;

    public LoginGuardDatabase(string? user = null, string? pass = null)
    {
        var connection_string = $"Data Source=file:loginguard{Guid.NewGuid():N}?mode=memory&cache=shared;Pooling=False";

        keeper = new SQLiteConnection(connection_string);
        keeper.Open();

        using var database = new Database(keeper);
        database.Execute(DDL_AGENCY);
        database.Execute(
            "INSERT INTO Agency (AgencyID, Title, Active, Domain, Link, UserName, Password) " +
            "VALUES (1, @title, 3, 'guard', 'https://guard.example.com/', @user, @pass)",
            new { title = LoginGuardAgency.Title, user, pass });

        Agency = new LoginGuardAgency { DatabaseFactory = new SharedFactory(connection_string) };
        Agency.LoadFromDatabase(database);
    }

    public LoginGuardAgency Agency { get; }

    public void Dispose()
    {
        keeper.Dispose();
    }

    private sealed class SharedFactory(string connection_string) : IDatabaseFactory
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
}

public class LoginPageGuardTests
{
    private const string login_url = "https://guard.example.com/login";

    private const string login_page = "<html><body>sign in</body></html>";

    [Fact]
    public void Null_credentials_wait_and_recheck_without_touching_the_form()
    {
        using var db = new LoginGuardDatabase();
        var page = new LoginGuardPage(db.Agency);

        var commands = page.IssueCommand(login_url, login_page);

        Assert.NotNull(commands);
        Assert.Equal(2, commands!.Length);
        Assert.Equal("wait", commands[0].Action);
        Assert.Equal(30_000, (int)commands[0].Params!["miliseconds"]);
        Assert.Equal("recheck", commands[1].Action);
        Assert.False(page.LoginCommandsCalled);
    }

    [Fact]
    public void Empty_credentials_wait_and_recheck_without_touching_the_form()
    {
        using var db = new LoginGuardDatabase(user: "user@example.com", pass: "");
        var page = new LoginGuardPage(db.Agency);

        var commands = page.IssueCommand(login_url, login_page);

        Assert.NotNull(commands);
        Assert.Equal(2, commands!.Length);
        Assert.Equal("wait", commands[0].Action);
        Assert.Equal("recheck", commands[1].Action);
        Assert.False(page.LoginCommandsCalled);
    }

    [Fact]
    public void Present_credentials_fill_the_form()
    {
        using var db = new LoginGuardDatabase(user: "user@example.com", pass: "secret");
        var page = new LoginGuardPage(db.Agency);

        var commands = page.IssueCommand(login_url, login_page);

        Assert.NotNull(commands);
        Assert.Equal(3, commands!.Length);
        Assert.Equal("fill", commands[0].Action);
        Assert.Equal("#user", commands[0].Object);
        Assert.Equal("user@example.com", (string)commands[0].Params!["value"]);
        Assert.Equal("secret", (string)commands[1].Params!["value"]);
        Assert.Equal("click", commands[2].Action);
        Assert.True(page.LoginCommandsCalled);
        Assert.Equal(("user@example.com", "secret"), page.ReceivedCredentials);
    }

    [Fact]
    public void Missing_credentials_flow_through_agency_analysis_as_login_wait()
    {
        using var db = new LoginGuardDatabase();

        var result = db.Agency.AnalyzeContent(login_url, login_page);

        Assert.Equal(TrendState.Login, result.State);
        Assert.Equal(2, result.Commands.Length);
        Assert.All(result.Commands, c => Assert.True(c.Action is "wait" or "recheck"));
    }
}
