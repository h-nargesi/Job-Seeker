using Photon.JobSeeker.Analyze.Models;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpatPageLogin : IamExpatPage
    {
        public override int Order => 1;

        public override TrendState TrendState => TrendState.Login;

        public IamExpatPageLogin(IamExpat parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_login_url.IsMatch(url)) return null;

            var (user, pass) = GetUserPass();

            return new Command[] {
                Command.Fill(@"input[id=""edit-name""]", user),
                Command.Fill(@"input[id=""edit-pass""]", pass),
                Command.Click(@"input[id=""edit-submit""]")
            };
        }
    }
}