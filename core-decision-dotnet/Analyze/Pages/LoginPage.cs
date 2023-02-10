namespace Photon.JobSeeker.Pages
{
    public abstract class LoginPage : PageBase
    {
        public override int Order => 1;

        public override TrendState TrendState => TrendState.Login;

        protected LoginPage(Agency parent, IPageHandler handler) : base(parent, handler) { }

        public abstract Command[] LoginCommands { get; }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (Handler.CheckUrl(content, out var command)) return command;

            var (user, pass) = GetUserPass();

            return LoginCommands;
        }
    }
}