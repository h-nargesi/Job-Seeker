namespace Photon.JobSeeker.Pages
{
    public abstract class OtherPages : PageBase
    {
        public override int Order => 100;

        public override TrendState TrendState => TrendState.Other;

        protected OtherPages(Agency parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            return new Command[0];
        }

        protected override bool CheckInvalidUrl(string text, out Command[]? commands)
        {
            throw new NotImplementedException();
        }
    }
}