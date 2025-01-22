using Photon.JobSeeker.Pages;
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Bayt;

class BaytPageSearch(Bayt parent) : SearchPage(parent), BaytPage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !BaytPage.reg_search_url.IsMatch(url);
    }

    protected override bool CheckInvalidSearchTitle(string url, string content, out Command[]? commands)
    {
        if (!BaytPage.reg_search_keywords_url.IsMatch(url) ||
            !BaytPage.reg_search_location_url.IsMatch(url))
        {
            commands = [Command.Go(Parent.SearchLink)];
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
        var job_matches = BaytPage.reg_job_url.Matches(content).Cast<Match>();

        foreach (var job_match in job_matches)
        {
            var code = job_match.Groups[2].Value;
            var url = string.Join("", Parent.BaseUrl, job_match.Groups[1].Value);
            result.Add((url, code));
        }

        return result;
    }

    protected override Command[] CheckNextButton(string url, string content)
    {
        var url_page_match = BaytPage.reg_search_url_page.Match(url);
        var current_page = (url_page_match.Success ? int.Parse(url_page_match.Groups[1].Value) + 1 : 2).ToString();

        var page_match = BaytPage.reg_search_next.Matches(content).Cast<Match>();
        foreach (var page in page_match)
        {
            if (page.Success && page.Groups[2].Value == current_page)
            {
                return [Command.Go(page.Groups[1].Value)];
            }
        }

        return [];
    }
}
