using System.Text.RegularExpressions;
using System.Web;
using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.IamExpat;

class IamExpatPageSearch(IamExpat parent) : SearchPage(parent), IamExpatPage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !IamExpatPage.reg_search_url.IsMatch(url);
    }

    protected override bool CheckInvalidSearchTitle(string url, string content, out Command[]? commands)
    {
        if (IamExpatPage.reg_search_category_url.IsMatch(url) &&
            Regex.IsMatch(url, GetSearchLocationUrlPattern(), RegexOptions.IgnoreCase))
        {
            commands = null;
            return false;
        }
        else
        {
            commands = [Command.Go(string.Concat(Parent.SearchLink, IamExpatPage.search_category_path))];
            return true;
        }
    }

    protected override IEnumerable<(string url, string code)> GetJobUrls(string url, string content)
    {
        var job_matches = IamExpatPage.reg_job_url.Matches(content).Cast<Match>();
        var baseUrl = PageUtils.GetBaseUrl(url);

        foreach (Match job_match in job_matches)
        {
            var jobCode = IamExpatPage.GetJobCode(job_match);
            var jobUrl = string.Concat(baseUrl, HttpUtility.HtmlDecode(job_match.Value));
            yield return (jobUrl, jobCode);
        }
    }

    protected override Command[] CheckNextButton(string url, string content)
    {
        if (!IamExpatPage.reg_job_url.IsMatch(content)) return [];

        var page = 1;
        var page_match = IamExpatPage.reg_search_page_param.Match(url);
        if (page_match.Success) page = int.Parse(page_match.Groups[1].Value);

        var next = page_match.Success
            ? Regex.Replace(url, @"([?&])page=\d+", $"$1page={page + 1}")
            : @$"{Parent.SearchLink}{IamExpatPage.search_category_path}?page={page + 1}";

        return [Command.Go(next)];
    }

    private string GetSearchLocationUrlPattern()
    {
        var tld = Parent.CurrentMethod.Url.Length < 3 ? string.Empty : Parent.CurrentMethod.Url[0..2];
        return string.Concat(IamExpatPage.reg_search_location_pattern, tld);
    }
}
