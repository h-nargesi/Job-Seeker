using Serilog;

namespace Photon.JobSeeker.Pages;

abstract class SearchPage(Agency parent) : PageBase(parent)
{
    public override int Order => 20;

    public override TrendState TrendState => TrendState.Seeking;

    public override Command[]? IssueCommand(string url, string content)
    {
        if (CheckInvalidUrl(url, content)) return null;

        if (CheckInvalidSearchTitle(url, content, out var commands)) return commands;

        var codes = new HashSet<string>();
        var region = Parent.ParseRegion(url);
        using var database = Parent.DatabaseFactory.Open();

        database.BeginTransaction();
        try
        {
            foreach (var (jobUrl, jobCode) in GetJobUrls(url, content))
            {
                if (string.IsNullOrEmpty(jobCode) || codes.Contains(jobCode)) continue;
                codes.Add(jobCode);

                database.Job.InsertFromSearch(Parent.ID, region.Title, jobUrl, jobCode);
            }

            database.Commit();
        }
        catch
        {
            database.Rollback();
            throw;
        }

        if (codes.Count == 0)
            Log.Warning("No jobs extracted from search results page ({0}): {1}", Parent.Name, url);

        return CheckNextButton(url, content) ?? [];
    }

    protected abstract bool CheckInvalidSearchTitle(string url, string content, out Command[]? commands);

    protected abstract IEnumerable<(string url, string code)> GetJobUrls(string pageUrl, string content);

    protected abstract Command[] CheckNextButton(string url, string content);
}
