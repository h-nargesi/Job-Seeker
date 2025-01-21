namespace Photon.JobSeeker.Pages;

abstract class SearchPage : PageBase
{
    public override int Order => 20;

    public override TrendState TrendState => TrendState.Seeking;

    protected SearchPage(Agency parent) : base(parent) { }

    public override Command[]? IssueCommand(string url, string content)
    {
        if (CheckInvalidUrl(url, content, out Command[]? commands)) return commands;

        if (CheckInvalidSearchTitle(content, out commands)) return commands;

        var codes = new HashSet<string>();
        using var database = Database.Open();

        foreach (var (link, code) in GetJobUrls(content))
        {
            if (string.IsNullOrEmpty(code) || codes.Contains(code)) continue;
            codes.Add(code);

            database.Job.Save(new
            {
                AgencyID = Parent.ID,
                Country = Parent.CurrentMethodTitle,
                Url = link,
                Code = code,
                State = JobState.Saved
            });
        }

        if (CheckNextButton(content, out commands)) return commands;
        else return Array.Empty<Command>();
    }

    protected abstract bool CheckInvalidSearchTitle(string text, out Command[]? commands);

    protected abstract IEnumerable<(string url, string code)> GetJobUrls(string text);

    protected abstract bool CheckNextButton(string text, out Command[]? commands);
}