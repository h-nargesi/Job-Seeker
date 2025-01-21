using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Indeed;

class IndeedPageAuth : AuthPage, IndeedPage
{
    public IndeedPageAuth(Indeed parent) : base(parent) { }

    protected override bool CheckInvalidUrl(string url, string content, out Command[]? commands)
    {
        commands = null;
        return !IndeedPage.reg_login_but.IsMatch(content);
    }

    protected override Command[] LoginUrl()
    {
        return new Command[] { Command.Click(@"a[href^=""https://secure.indeed.com/account/login""]") };
    }
}