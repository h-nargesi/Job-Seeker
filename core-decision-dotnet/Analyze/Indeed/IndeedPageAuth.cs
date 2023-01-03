namespace Photon.JobSeeker.Indeed
{
    class IndeedPageAuth : IndeedPage
    {
        public override int Order => 2;

        public override TrendState TrendState => TrendState.Auth;

        public IndeedPageAuth(Indeed parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_login_but.IsMatch(content)) return null;

            return new Command[] { Command.Click(@"a[href^=""https://secure.indeed.com/account/login""]") };
        }
    }
}