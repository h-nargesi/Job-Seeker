using System.Text.RegularExpressions;
using System.Web;

namespace Photon.JobSeeker.Indeed;

public static class IndeedJob
{
    private static readonly Regex title_heading = new(
        @"<h\d(?=[^>]*data-testid=""vj-job-title"")[^>]*>\s*(?:<span[^>]*>)?([^<]*?)(?:</span>)?\s*</h\d>",
        RegexOptions.IgnoreCase);

    public static string? ExtractTitle(string html)
    {
        return Extract(title_heading, html);
    }

    private static string? Extract(Regex regex, string html)
    {
        var match = regex.Match(html);
        if (!match.Success) return null;

        var title = HttpUtility.HtmlDecode(match.Groups[1].Value).Trim();
        return string.IsNullOrEmpty(title) ? null : title;
    }
}
