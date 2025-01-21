namespace Photon.JobSeeker.Pages;

public abstract class OtherPages : Page
{
    public override int Order => 100;

    public override TrendState TrendState => TrendState.Other;

    protected OtherPages(Agency parent) : base(parent) { }

    public override Command[]? IssueCommand(string url, string content)
    {
        return Array.Empty<Command>();
    }
}