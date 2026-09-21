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
        if (IamExpatPage.reg_search_category_url.IsMatch(url))
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

    protected override IEnumerable<(string url, string code)> GetJobUrls(string content)
    {
        var job_matches = IamExpatPage.reg_job_url.Matches(content).Cast<Match>();

        foreach (Match job_match in job_matches)
        {
            var code = IamExpatPage.GetJobCode(job_match);
            var url = string.Concat(Parent.BaseUrl, HttpUtility.HtmlDecode(job_match.Value));
            yield return (url, code);
        }
    }

    protected override Command[] CheckNextButton(string url, string content)
    {
        if (!IamExpatPage.reg_job_url.IsMatch(content)) return Array.Empty<Command>();

        var page = 1;
        var page_match = IamExpatPage.reg_search_page_param.Match(url);
        if (page_match.Success) page = int.Parse(page_match.Groups[1].Value);

        return [Command.Go(@$"{Parent.SearchLink}{IamExpatPage.search_category_path}?page={page + 1}")];
    }
}
