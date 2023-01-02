namespace Photon.JobSeeker.IamExpat
{
    class IndeedPageAuth : IndeedPage
    {
        public override int Order => 2;

        public override TrendState TrendState => TrendState.Auth;

        public IndeedPageAuth(IamExpat parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_login_but.IsMatch(content)) return null;

            return new Command[] { Command.Click(@"a[href=""/login""]") };
        }
    }
}