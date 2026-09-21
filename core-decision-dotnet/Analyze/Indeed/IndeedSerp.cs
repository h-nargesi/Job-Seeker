using System.Text.RegularExpressions;
using System.Web;

namespace Photon.JobSeeker.Indeed;

public static class IndeedSerp
{
    public static readonly Regex reg_viewjob_link =
        new(@"[""'](/(?:m/)?viewjob\?jk=(\w+)[^""']*)[""']", RegexOptions.IgnoreCase);

    public static readonly Regex reg_clk_link =
        new(@"[""'](/rc/clk\?jk=(\w+)[^""']*)[""']", RegexOptions.IgnoreCase);

    public static IEnumerable<(string url, string code)> ExtractJobLinks(string html)
    {
        var codes = new HashSet<string>();

        foreach (var (url, code) in Extract(reg_viewjob_link, html))
        {
            if (codes.Add(code)) yield return ($"/viewjob?jk={code}", code);
        }

        foreach (var (url, code) in Extract(reg_clk_link, html))
        {
            if (codes.Add(code)) yield return ($"/viewjob?jk={code}", code);
        }
    }

    private static IEnumerable<(string url, string code)> Extract(Regex regex, string html)
    {
        foreach (Match match in regex.Matches(html))
            yield return (HttpUtility.HtmlDecode(match.Groups[1].Value), match.Groups[2].Value);
    }
}
