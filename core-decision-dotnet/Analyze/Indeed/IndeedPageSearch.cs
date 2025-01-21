using Photon.JobSeeker.Pages;
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed;

class IndeedPageSearch : SearchPage, IndeedPage
{
    public IndeedPageSearch(Indeed parent) : base(parent) { }

    protected override bool CheckInvalidUrl(string url, string content, out Command[]? commands)
    {
        commands = null;
        return !IndeedPage.reg_search_url.IsMatch(url);
    }

    protected override bool CheckInvalidSearchTitle(string url, string content, out Command[]? commands)
    {
        if (IndeedPage.reg_search_keywords_url.IsMatch(url))
        {
            commands = null;
            return true;
        }
        else
        {
            commands = new[] { Command.Go(@$"/jobs?q=developer") };
            return true;
        }
    }

    protected override IEnumerable<(string url, string code)> GetJobUrls(string text)
    {
        var result = new List<(string url, string code)>();
        var job_matches = IndeedPage.reg_job_url.Matches(text).Cast<Match>();

        foreach (Match job_match in job_matches)
        {
            var code = job_match.Groups[1].Value;
            var url = string.Join("", Parent.Link, "/viewjob?jk=", code);
            result.Add((url, code));
        }

        return result;
    }

    protected override bool CheckNextButton(string text, out Command[]? commands)
    {
        if (IndeedPage.reg_search_end.IsMatch(text))
        {
            commands = new[] { Command.Click(@"a[aria-label=""Next Page""]") };
            return true;
        }
        else
        {
            commands = null;
            return false;
        }
    }
}