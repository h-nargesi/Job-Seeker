using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Bayt;

class BaytPageAuth : AuthPage, BaytPage
{
    public BaytPageAuth(Bayt parent) : base(parent) { }

    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !BaytPage.reg_login_but.IsMatch(content);
    }

    protected override Command[] LoginUrl()
    {
        return new Command[] { Command.Click(@"a[href^=""https://www.bayt.com/en/login/""]") };
    }
}
