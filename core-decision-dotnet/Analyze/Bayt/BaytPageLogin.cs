using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Bayt;

class BaytPageLogin(Bayt parent) : LoginPage(parent), BaytPage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !BaytPage.reg_login_url.IsMatch(url);
    }

    protected override Command[] LoginCommands()
    {
        var (user, pass) = GetUserPass();

        return [
            Command.Fill(@"#LoginForm_username", user),
            Command.Fill(@"#LoginForm_password", pass),
            Command.Click(@"#login-button"),
        ];
    }
}