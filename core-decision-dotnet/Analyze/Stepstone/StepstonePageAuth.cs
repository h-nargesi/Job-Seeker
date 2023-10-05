namespace Photon.JobSeeker.Stepstone
{
    class StepstonePageAuth : StepstonePage
    {
        public override int Order => 2;

        public override TrendState TrendState => TrendState.Auth;

        public StepstonePageAuth(Stepstone parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (reg_login_profile.IsMatch(url))
            {
                return new Command[] { Command.Go(parent.SearchLink) };
            }

            if (!reg_login_but.IsMatch(content))
            {
                return null;
            }

            return new Command[] { Command.Go(@"/candidate/login") };
        }
    }
}