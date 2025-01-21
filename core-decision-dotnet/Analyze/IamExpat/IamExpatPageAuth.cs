using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.IamExpat;

class IamExpatPageAuth : AuthPage, IamExpatPage
{
    public IamExpatPageAuth(IamExpat parent) : base(parent) { }

    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !IamExpatPage.reg_login_but.IsMatch(content);
    }

    protected override Command[] LoginUrl() => new Command[]
    {
        Command.Click(@"a[href=""/login""]")
    };
}