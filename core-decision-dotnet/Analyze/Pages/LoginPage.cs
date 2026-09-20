namespace Photon.JobSeeker.Pages;

public abstract class LoginPage : PageBase
{
    public override int Order => 1;

    public override TrendState TrendState => TrendState.Login;

    protected LoginPage(Agency parent) : base(parent) { }

    public override Command[]? IssueCommand(string url, string content)
    {
        if (CheckInvalidUrl(url, content)) return null;

        var (user, pass) = GetUserPass();

        if (LoginCredentialsMissing(user, pass)) return MissingCredentialsCommands();

        return LoginCommands(user, pass);
    }

    protected abstract Command[] LoginCommands(string user, string pass);
}