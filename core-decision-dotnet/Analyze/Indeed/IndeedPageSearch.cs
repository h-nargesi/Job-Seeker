using Photon.JobSeeker.Pages;

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
        foreach (var code in IndeedSerp.ExtractJobCodes(content))
            yield return (string.Join("", Parent.BaseUrl, "/viewjob?jk=", code), code);
    }

    protected override Command[] CheckNextButton(string url, string text)
    {
        if (!IndeedPage.reg_search_end.IsMatch(text)) return [];
        return [Command.Click(@"a[aria-label=""Next Page""]")];
    }
}