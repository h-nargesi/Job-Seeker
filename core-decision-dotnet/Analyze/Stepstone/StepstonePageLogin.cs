namespace Photon.JobSeeker.Stepstone
{
    class StepstonePageLogin : StepstonePage
    {
        public override int Order => 1;

        public override TrendState TrendState => TrendState.Login;

        public StepstonePageLogin(Stepstone parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_login_url.IsMatch(url)) return null;

            var (user, pass) = GetUserPass();

            return new Command[] {
                Command.Fill(@"[name=""email""]", user),
                Command.Fill(@"[name=""password""]", pass),
                Command.Click(@"button[type=""submit""]")
            };
        }
    }
}