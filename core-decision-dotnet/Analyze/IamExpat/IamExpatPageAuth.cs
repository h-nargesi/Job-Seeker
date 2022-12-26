using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpatPageAuth : IamExpatPage
    {
        public override int Order => 2;

        public override TrendType TrendType => TrendType.None;

        private static readonly Regex reg_login_but = new(@"<a[^>]+href=[""\']/login[""\']");

        public IamExpatPageAuth(IamExpat parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_login_but.IsMatch(content)) return null;

            return new Command[] { Command.Click(@"a[href=""/login""]") };
        }
    }
}