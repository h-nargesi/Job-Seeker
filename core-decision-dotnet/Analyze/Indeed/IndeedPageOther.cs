using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Indeed;

class IndeedPageOther : OtherPages, IndeedPage
{
    public IndeedPageOther(Indeed parent) : base(parent) { }

    public override Command[]? IssueCommand(string url, string content)
    {
        return Array.Empty<Command>();
    }
}