namespace Photon.JobSeeker.Pages
{
    public abstract class OtherPages : PageBase
    {
        public override int Order => 100;

        public override TrendState TrendState => TrendState.Other;

        protected OtherPages(Agency parent, IPageHandler handler) : base(parent, handler) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            return new Command[0];
        }
    }
}