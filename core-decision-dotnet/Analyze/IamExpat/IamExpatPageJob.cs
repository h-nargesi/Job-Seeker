using System.Text.RegularExpressions;

class IamExpatPageJob : IamExpatPage
{
    public override int Order => 10;

    private static readonly Regex reg_job_url = new(@"/career/jobs-[^""\']+/it-technology/[^""\']+/(\d+)/?");

    public IamExpatPageJob(Agency parent) : base(parent)
    {
    }

    public override Command[]? IssueCommand(string url, string content)
    {
        if (reg_job_url.IsMatch(content)) return null;

        return Array.Empty<Command>();
    }

}