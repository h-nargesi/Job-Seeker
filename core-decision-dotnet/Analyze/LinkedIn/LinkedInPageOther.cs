namespace Photon.JobSeeker.LinkedIn
{
    class LinkedInPageOther : LinkedInPage
    {
        public override int Order => 100;

        public override TrendType TrendType => TrendType.Searching;

        public LinkedInPageOther(LinkedIn parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            return new Command[] { Command.Go("https://www.linkedin.com/jobs/search/") };
        }
    }
}