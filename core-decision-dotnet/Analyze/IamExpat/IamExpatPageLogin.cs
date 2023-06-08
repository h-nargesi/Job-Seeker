using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpatPageLogin : LoginPage, IamExpatPageInterface
    {
        public override int Order => 1;

        public override TrendState TrendState => TrendState.Login;

        public IamExpatPageLogin(IamExpat parent) : base(parent) { }

        protected override bool CheckInvalidUrl(string text, out Command[]? commands)
        {
            commands = null;
            return !IamExpatPageInterface.reg_login_url.IsMatch(text);
        }

        protected override Command[] LoginCommands()
        {
            var (user, pass) = GetUserPass();

            return new Command[]
            {
                Command.Fill(@"#edit-name", user),
                Command.Fill(@"#edit-pass", pass),
                Command.Click(@"#edit-submit")
            };
        }
    }
}