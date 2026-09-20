using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Stepstone;

class StepstonePageLogin(Stepstone parent) : LoginPage(parent), StepstonePage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !StepstonePage.reg_login_url.IsMatch(url);
    }

    protected override Command[] LoginCommands(string user, string pass)
    {
        return
        [
            Command.Fill(@"[name=""email""]", user),
            Command.Fill(@"[name=""password""]", pass),
            Command.Click(@"button[type=""submit""]"),
        ];
    }
}
