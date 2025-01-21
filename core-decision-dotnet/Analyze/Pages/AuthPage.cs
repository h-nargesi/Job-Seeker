namespace Photon.JobSeeker.Pages;

public abstract class AuthPage : PageBase
{
    public override int Order => 2;

    public override TrendState TrendState => TrendState.Auth;

    protected AuthPage(Agency parent) : base(parent) { }
    
    public override Command[]? IssueCommand(string url, string content)
    {
        if (CheckInvalidUrl(url, content)) return null;

        return LoginUrl();
    }
    
    protected abstract Command[] LoginUrl();
}