using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpatPageAuth : AuthPage, IamExpatPageInterface
    {
        public override int Order => 2;

        public override TrendState TrendState => TrendState.Auth;

        public IamExpatPageAuth(IamExpat parent) : base(parent) { }

        protected override bool CheckInvalidUrl(string text, out Command[]? commands)
        {
            commands = null;
            return !IamExpatPageInterface.reg_login_but.IsMatch(text);
        }

        protected override Command[] LoginUrl() => new Command[]
        {
            Command.Click(@"a[href=""/login""]")
        };
    }
}