using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Indeed;

class IndeedPageLogin(Indeed parent) : LoginPage(parent), IndeedPage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !IndeedPage.reg_login_url.IsMatch(url);
    }

    protected override Command[] LoginCommands()
    {
        var (user, pass) = GetUserPass();

        return [
            Command.Fill(@"#ifl-InputFormField-3", user),
            Command.Click(@"#auth-page-google-password-fallback"),
            Command.Fill(@"#ifl-InputFormField-16", pass),
            Command.Click(@"button[type=""submit""]")
        ];
    }
}