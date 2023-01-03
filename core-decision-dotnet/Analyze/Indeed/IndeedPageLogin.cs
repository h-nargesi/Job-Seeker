namespace Photon.JobSeeker.Indeed
{
    class IndeedPageLogin : IndeedPage
    {
        public override int Order => 1;

        public override TrendState TrendState => TrendState.Login;

        public IndeedPageLogin(Indeed parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_login_url.IsMatch(url)) return null;

            var (user, pass) = GetUserPass();

            // TODO: complete the page
            return new Command[] {
                Command.Fill(@"input[id=""edit-name""]", user),
                Command.Fill(@"input[id=""edit-pass""]", pass),
                Command.Click(@"input[id=""edit-submit""]")
            };
        }
    }
}