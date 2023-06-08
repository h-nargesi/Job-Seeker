namespace Photon.JobSeeker.Pages
{
    public abstract class LoginPage : PageBase
    {
        public override int Order => 1;

        public override TrendState TrendState => TrendState.Login;

        protected LoginPage(Agency parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (CheckInvalidUrl(content, out var command)) return command;

            return LoginCommands();
        }

        protected abstract Command[] LoginCommands();
    }
}