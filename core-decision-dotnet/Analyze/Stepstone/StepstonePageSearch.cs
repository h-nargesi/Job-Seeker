using Photon.JobSeeker.Pages;
using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Stepstone;

class StepstonePageSearch(Stepstone parent) : SearchPage(parent), StepstonePage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !StepstonePage.reg_search_url.IsMatch(url);
    }

    protected override bool CheckInvalidSearchTitle(string url, string content, out Command[]? commands)
    {
        if (StepstonePage.reg_search_keywords_url_title.IsMatch(url) &&
            StepstonePage.reg_search_keywords_url_en.IsMatch(url) &&
            StepstonePage.reg_search_keywords_url_type.IsMatch(url))
        {
            commands = null;
            return false;
        }

        commands = [Command.Go(Parent.SearchLink)];
        return true;
    }

    protected override IEnumerable<(string url, string code)> GetJobUrls(string content)
    {
        foreach (Match job_match in StepstonePage.reg_job_url.Matches(content).Cast<Match>())
            yield return (string.Join("", Parent.BaseUrl, job_match.Value), job_match.Groups[1].Value);
    }

    protected override Command[] CheckNextButton(string url, string content)
    {
        if (!StepstonePage.reg_search_end.IsMatch(content)) return [];

        return
        [
            Command.Click(@"a[aria-label=""Next""]"),
            Command.Wait(3000),
            Command.Reload(),
        ];
    }
}
