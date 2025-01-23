using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.IamExpat;

class IamExpatPageAuth(IamExpat parent) : AuthPage(parent), IamExpatPage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !IamExpatPage.reg_login_but.IsMatch(content);
    }

    protected override Command[] LoginUrl() =>
    [
        Command.Click(@"a[href=""/login""]")
    ];
}