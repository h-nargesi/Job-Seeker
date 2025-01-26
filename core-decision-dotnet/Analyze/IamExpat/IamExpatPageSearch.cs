using System.Text.RegularExpressions;
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
        if (IamExpatPage.reg_search_title.IsMatch(content))
        {
            commands = null;
            return false;
        }
        else
        {
            commands = FillSearchCommands();
            return true;
        }
    }

    protected override IEnumerable<(string url, string code)> GetJobUrls(string content)
    {
        var result = new List<(string url, string code)>();
        var job_matches = IamExpatPage.reg_job_url.Matches(content).Cast<Match>();

        foreach (Match job_match in job_matches)
        {
            var code = IamExpatPage.GetJobCode(job_match);
            var url = string.Join("", Parent.BaseUrl, job_match.Value);
            result.Add((url, code));
        }

        return result;
    }

    protected override Command[] CheckNextButton(string url, string text)
    {
        if (!IamExpatPage.reg_search_end.IsMatch(text)) return Array.Empty<Command>();
        return FillSearchCommands();
    }

    private static Command[] FillSearchCommands() =>
    [
        Command.Click(@"label[for=""industry-260""]"), // it-technology
        Command.Click(@"label[for=""ccareer-level-19926""]"), // entry-level
        Command.Click(@"label[for=""career-level-19928""]"), // experienced
        Command.Click(@"label[for=""contract-19934""]"),
        Command.Wait(3000),
        Command.Click(@"input[type=""submit""][value=""Search""]"),
    ];
}