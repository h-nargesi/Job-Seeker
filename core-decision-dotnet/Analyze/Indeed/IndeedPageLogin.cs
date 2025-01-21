using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Indeed;

class IndeedPageLogin : LoginPage, IndeedPage
{
    public IndeedPageLogin(Indeed parent) : base(parent) { }

    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !IndeedPage.reg_login_url.IsMatch(url);
    }

    protected override Command[] LoginCommands()
    {
        var (user, pass) = GetUserPass();

        return new Command[] {
            Command.Fill(@"#ifl-InputFormField-3", user),
            Command.Click(@"#auth-page-google-password-fallback"),
            Command.Fill(@"#ifl-InputFormField-16", pass),
            Command.Click(@"button[type=""submit""]")
        };
    }
}