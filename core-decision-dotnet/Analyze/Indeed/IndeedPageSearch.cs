using Photon.JobSeeker.Pages;
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed;

class IndeedPageSearch(Indeed parent) : SearchPage(parent), IndeedPage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !IndeedPage.reg_search_url.IsMatch(url);
    }

    protected override bool CheckInvalidSearchTitle(string url, string content, out Command[]? commands)
    {
        if (IndeedPage.reg_search_keywords_url.IsMatch(url))
        {
            commands = null;
            return false;
        }
        else
        {
            commands = [Command.Go(@$"/jobs?q={Agency.SearchTitle}")];
            return true;
        }
    }

    protected override IEnumerable<(string url, string code)> GetJobUrls(string content)
    {
        var result = new List<(string url, string code)>();
        var job_matches = IndeedPage.reg_job_url.Matches(content).Cast<Match>();

        foreach (Match job_match in job_matches)
        {
            var code = job_match.Groups[1].Value;
            var url = string.Join("", Parent.Link, "/viewjob?jk=", code);
            result.Add((url, code));
        }

        return result;
    }

    protected override Command[] CheckNextButton(string url, string text)
    {
        if (!IndeedPage.reg_search_end.IsMatch(text)) return [];
        return [Command.Click(@"a[aria-label=""Next Page""]")];
    }
}