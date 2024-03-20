using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn;

class LinkedInPageSearch : LinkedInPage
{
    public override int Order => 20;

    public override TrendState TrendState => TrendState.Seeking;

    public LinkedInPageSearch(LinkedIn parent) : base(parent) { }

    public override Command[]? IssueCommand(string url, string content)
    {
        if (!reg_search_url.IsMatch(url)) return null;

        if (!reg_search_keywords_url.IsMatch(url) ||
            !reg_search_location_url.IsMatch(url) ||
            !reg_search_options_url.IsMatch(url))
        {
            return new Command[]
            {
                    Command.Go(@$"/jobs/search/?f_E=3%2C4&keywords={Agency.SearchTitle}&location={parent.RunningUrl}&refresh=true"),
            };
        }

        var codes = new HashSet<long>();
        using var database = Database.Open();

        foreach (Match job_match in reg_job_url.Matches(content).Cast<Match>())
        {
            var code = long.Parse(job_match.Groups[1].Value);

            if (codes.Contains(code)) continue;
            codes.Add(code);

            database.Job.Save(new
            {
                AgencyID = parent.ID,
                Url = string.Join("", parent.BaseUrl, job_match.Value),
                Country = parent.CurrentMethodTitle,
                Code = code.ToString(),
                State = JobState.Saved
            });
        }

        return GetNextPageButton(content);
    }

    private static Command[] GetNextPageButton(string content)
    {
        var match = reg_search_page_panel.Match(content);
        if (!match.Success) return Array.Empty<Command>();
        content = content[(match.Index + match.Length)..];

        match = reg_search_page_panel_end.Match(content);
        if (!match.Success) return Array.Empty<Command>();
        content = content[..match.Index];

        match = reg_search_current_page.Match(content);
        if (!match.Success) return Array.Empty<Command>();
        content = content[(match.Index + match.Length)..];

        match = reg_search_other_page.Match(content);
        if (!match.Success) return Array.Empty<Command>();

        return new Command[]
        {
            Command.Click(@$"button[aria-label=""{match.Groups[1].Value}""]"),
            Command.Recheck(),
        };
    }
}
