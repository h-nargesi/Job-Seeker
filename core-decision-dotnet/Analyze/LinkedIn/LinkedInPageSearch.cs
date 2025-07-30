using Photon.JobSeeker.Pages;
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn;

class LinkedInPageSearch(LinkedIn parent) : SearchPage(parent), LinkedInPage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !LinkedInPage.reg_search_url.IsMatch(url);
    }

    protected override bool CheckInvalidSearchTitle(string url, string content, out Command[]? commands)
    {
        if (!LinkedInPage.reg_search_keywords_url.IsMatch(url) ||
            !LinkedInPage.reg_search_location_url.IsMatch(url) ||
            !LinkedInPage.reg_search_options_url.IsMatch(url))
        {
            var parent = Parent as LinkedIn;
            commands =
            [
                Command.Go(@$"/jobs/search/?keywords={Agency.SearchTitle}&refresh=true{parent?.RunningUrl}"),
            ];
            return true;
        }
        else
        {
            commands = null;
            return false;
        }
    }

    protected override IEnumerable<(string url, string code)> GetJobUrls(string content)
    {
        var result = new List<(string url, string code)>();
        var job_matches = LinkedInPage.reg_job_url.Matches(content).Cast<Match>();

        foreach (Match job_match in job_matches)
        {
            var code = job_match.Groups[1].Value;
            var url = string.Join("", Parent.BaseUrl, job_match.Value);
            result.Add((url, code));
        }

        return result;
    }

    protected override Command[] CheckNextButton(string url, string content)
    {
        var match = LinkedInPage.reg_search_page_panel.Match(content);
        if (!match.Success) return [];
        content = content[(match.Index + match.Length)..];

        match = LinkedInPage.reg_search_page_panel_end.Match(content);
        if (!match.Success) return [];
        content = content[..match.Index];

        match = LinkedInPage.reg_search_current_page.Match(content);
        if (!match.Success) return [];
        content = content[(match.Index + match.Length)..];

        match = LinkedInPage.reg_search_other_page.Match(content);
        if (!match.Success) return [];

        return
        [
            Command.Click(@$"button[aria-label=""{match.Groups[1].Value}""]"),
            Command.Recheck(),
        ];
    }
}
