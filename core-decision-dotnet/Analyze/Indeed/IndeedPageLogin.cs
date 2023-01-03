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
                Command.Fill(@"#ifl-InputFormField-3", user),
                // Command.Click(@"#auth-page-google-password-fallback"),
                // Command.Fill(@"#ifl-InputFormField-16", pass),
                Command.Click(@"button[type=""submit""]")
            };
        }
    }
}