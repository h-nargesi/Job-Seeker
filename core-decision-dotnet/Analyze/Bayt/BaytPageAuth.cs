namespace Photon.JobSeeker.Bayt;

class BaytPageAuth : BaytPage
{
    public override int Order => 2;

    public override TrendState TrendState => TrendState.Auth;

    public BaytPageAuth(Bayt parent) : base(parent) { }

    public override Command[]? IssueCommand(string url, string content)
    {
        if (!reg_login_but.IsMatch(content)) return null;

        return new Command[] { Command.Click(@"a[href^=""https://www.bayt.com/en/login/""]") };
    }
}
