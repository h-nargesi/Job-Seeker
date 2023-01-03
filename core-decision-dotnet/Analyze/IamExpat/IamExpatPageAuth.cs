using Photon.JobSeeker.Analyze.Models;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpatPageAuth : IamExpatPage
    {
        public override int Order => 2;

        public override TrendState TrendState => TrendState.Auth;

        public IamExpatPageAuth(IamExpat parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_login_but.IsMatch(content)) return null;

            return new Command[] { Command.Click(@"a[href=""/login""]") };
        }
    }
}