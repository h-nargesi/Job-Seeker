using System.Text.RegularExpressions;
using System.Web;

namespace Photon.JobSeeker.Indeed;

public static class IndeedSerp
{
    public static readonly Regex reg_viewjob_link =
        new(@"[""'](/(?:m/)?viewjob\?jk=(\w+)[^""']*)[""']", RegexOptions.IgnoreCase);

    public static readonly Regex reg_clk_link =
        new(@"[""'](/rc/clk\?jk=(\w+)[^""']*)[""']", RegexOptions.IgnoreCase);

    public static readonly Regex reg_link_next =
        new(@"<link\b(?=[^>]*\brel=[""']next[""'])[^>]*\bhref=[""'](?!#)([^""']+)[""']", RegexOptions.IgnoreCase);

    public static readonly Regex reg_next_page_anchor =
        new(@"<a\b(?=[^>]*\b(?:data-testid=[""'][^""']*next[^""']*[""']|aria-label=[""']Next Page[""']))[^>]*>", RegexOptions.IgnoreCase);

    private static readonly Regex reg_next_page_label =
        new(@"""Next [Pp]age""\s*:\s*\[\s*null\s*,\s*""([^""]+)""");

    public const string next_page_selector = @"a[data-testid*=""next""], a[aria-label=""Next Page""]";

    public static string? FindNextPageLink(string html)
    {
        var match = reg_link_next.Match(html);
        return match.Success ? HttpUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    public static string? FindNextPageSelector(string html)
    {
        if (reg_next_page_anchor.IsMatch(html)) return next_page_selector;

        var label = NextPageLabel(html);
        if (label != null && AnchorWithLabel(html, label)) return $@"a[aria-label=""{label}""]";

        return null;
    }

    private static string? NextPageLabel(string html)
    {
        var match = reg_next_page_label.Match(html);
        return match.Success ? HttpUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    private static bool AnchorWithLabel(string html, string label)
    {
        var regex = new Regex($@"<a\b[^>]*\baria-label=[""']{Regex.Escape(label)}[""']", RegexOptions.IgnoreCase);
        return regex.IsMatch(html);
    }

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
