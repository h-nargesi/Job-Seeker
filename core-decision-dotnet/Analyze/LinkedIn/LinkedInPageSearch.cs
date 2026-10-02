using Photon.JobSeeker.Pages;
using System.Text.RegularExpressions;
using System.Web;

namespace Photon.JobSeeker.LinkedIn;

class LinkedInPageSearch(LinkedIn parent) : SearchPage(parent), LinkedInPage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !LinkedInPage.reg_search_url.IsMatch(url);
    }

    protected override bool CheckInvalidSearchTitle(string url, string content, out Command[]? commands)
    {
        var location_pattern = GetSearchLocationUrlPattern();

        if (!LinkedInPage.reg_search_keywords_url.IsMatch(url) ||
            location_pattern == null ||
            !Regex.IsMatch(url, location_pattern, RegexOptions.IgnoreCase) ||
            !LinkedInPage.reg_search_options_url.IsMatch(url))
        {
            commands =
            [
                Command.Go(@$"/jobs/search/?keywords={Agency.SearchTitle}&refresh=true{Parent.CurrentMethod.Url}"),
            ];
            return true;
        }
        else
        {
            commands = null;
            return false;
        }
    }

    protected override IEnumerable<(string url, string code)> GetJobUrls(string url, string content)
    {
        var result = new List<(string url, string code)>();
        var job_matches = LinkedInPage.reg_job_url.Matches(content).Cast<Match>();
        var baseUrl = PageUtils.GetBaseUrl(url);

        foreach (Match job_match in job_matches)
        {
            var jobCode = job_match.Groups[1].Value;
            var jobUrl = string.Join("", baseUrl, HttpUtility.HtmlDecode(job_match.Value));
            result.Add((jobUrl, jobCode));
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

    private string? GetSearchLocationUrlPattern()
    {
        var pattern = LinkedInPage.GetSearchLocationUrlPattern(Parent.CurrentMethod.Url);

        return pattern == null
            ? null
            : @$"(^|[?&]){pattern.Value.parameter}={pattern.Value.location}(&|$)";
    }
}
