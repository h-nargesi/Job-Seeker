namespace Photon.JobSeeker.LinkedIn
{
    class LinkedInPageOther : LinkedInPage
    {
        public override int Order => 100;

        public override TrendState TrendState => TrendState.Other;

        public LinkedInPageOther(LinkedIn parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            return Array.Empty<Command>();
        }
    }
}