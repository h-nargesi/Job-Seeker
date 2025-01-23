using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Indeed;

class IndeedPageAuth(Indeed parent) : AuthPage(parent), IndeedPage
{
    protected override bool CheckInvalidUrl(string url, string content) => !IndeedPage.reg_login_but.IsMatch(content);

    protected override Command[] LoginUrl() => [Command.Click(@"a[href^=""https://secure.indeed.com/auth""]")];
}