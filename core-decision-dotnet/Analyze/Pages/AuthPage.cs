namespace Photon.JobSeeker.Pages
{
    public abstract class AuthPage : PageBase
    {
        public override int Order => 2;

        public override TrendState TrendState => TrendState.Auth;

        protected AuthPage(Agency parent, IPageHandler handler) : base(parent, handler) { }

        public abstract Command[] LoginUrl { get; }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (Handler.CheckUrl(content, out var command)) return command;

            return LoginUrl;
        }
    }
}