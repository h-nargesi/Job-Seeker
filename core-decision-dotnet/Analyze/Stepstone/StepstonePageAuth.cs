using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Stepstone;

class StepstonePageAuth(Stepstone parent) : AuthPage(parent), StepstonePage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !StepstonePage.reg_login_but.IsMatch(content);
    }

    protected override Command[] LoginUrl()
    {
        return [Command.Go(@"/candidate/login")];
    }
}
