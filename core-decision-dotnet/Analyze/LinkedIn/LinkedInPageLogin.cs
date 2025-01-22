using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.LinkedIn;

class LinkedInPageLogin : LoginPage, LinkedInPage
{
    public LinkedInPageLogin(LinkedIn parent) : base(parent) { }

    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !LinkedInPage.reg_login_but.IsMatch(content);
    }

    protected override Command[] LoginCommands()
    {
        var (user, pass) = GetUserPass();

        return new Command[] {
            Command.Fill(@"#session_key", user),
            Command.Fill(@"#session_password", pass),
            Command.Click(@"button[type=""submit""]")
        };
    }
}