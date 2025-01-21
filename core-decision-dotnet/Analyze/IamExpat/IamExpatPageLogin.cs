using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.IamExpat;

class IamExpatPageLogin : LoginPage, IamExpatPage
{
    public IamExpatPageLogin(IamExpat parent) : base(parent) { }

    protected override bool CheckInvalidUrl(string url, string content, out Command[]? commands)
    {
        commands = null;
        return !IamExpatPage.reg_login_url.IsMatch(content);
    }

    protected override Command[] LoginCommands()
    {
        var (user, pass) = GetUserPass();

        return new Command[]
        {
            Command.Fill(@"#edit-name", user),
            Command.Fill(@"#edit-pass", pass),
            Command.Click(@"#edit-submit")
        };
    }
}