namespace Photon.JobSeeker.Indeed
{
    class IndeedPageOther : IndeedPage
    {
        public override int Order => 100;

        public override TrendState TrendState => TrendState.Other;

        public IndeedPageOther(Indeed parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            return new Command[] { Command.Go(parent.SearchDomain + "jobs?q=developer") };
        }
    }
}