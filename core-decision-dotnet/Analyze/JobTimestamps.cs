using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;

namespace Photon.JobSeeker;

public static class JobTimestamps
{
    private static readonly Regex exact_posted = new(
        @"datePosted""\s*:\s*""(\d{4}-\d{2}-\d{2}T[^""]+?)""",
        RegexOptions.IgnoreCase);

    private static readonly Regex html_marker = new(
        @"<p\b",
        RegexOptions.IgnoreCase);

    private static readonly Regex paragraph = new(
        @"<p\b[^>]*>.*?</p>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex anchor = new(
        @"<a\b[^>]*>.*?</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex inner_anchor = new(
        @"<a\b",
        RegexOptions.IgnoreCase);

    private static readonly Regex tag = new(@"<[^>]+>");

    private static readonly Regex prefixed_age = new(
        @"^\s*(?:re)?posted\s+(\d+)\s+(minutes?|hours?|days?|weeks?|months?)\s+ago\b",
        RegexOptions.IgnoreCase);

    private static readonly Regex bare_age = new(
        @"^\s*(\d+)\s+(minutes?|hours?|days?|weeks?|months?)\s+ago\s*$",
        RegexOptions.IgnoreCase);

    private static readonly Regex zero_age = new(
        @"^\s*(?:just posted|today)\s*$",
        RegexOptions.IgnoreCase);

    public static bool TryExtractExact(string? html, out DateTime? value)
    {
        value = null;
        if (string.IsNullOrEmpty(html)) return false;

        var match = exact_posted.Match(html);
        if (!match.Success) return false;

        if (!DateTimeOffset.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return false;

        value = DateTime.SpecifyKind(parsed.UtcDateTime, DateTimeKind.Unspecified);
        return true;
    }

    public static bool TryExtractRelative(string? content, DateTime scrapedAtUtc, out DateTime? value)
    {
        if (string.IsNullOrEmpty(content))
        {
            value = null;
            return false;
        }

        return html_marker.IsMatch(content)
            ? TryExtractRelativeHtml(content, scrapedAtUtc, out value)
            : TryExtractRelativeText(content, scrapedAtUtc, out value);
    }

    public static DateTime? Merge(DateTime? current, DateTime? exact, DateTime? relative)
    {
        if (exact != null) return exact;
        if (current == null && relative != null) return relative;
        return current;
    }

    private static bool TryExtractRelativeHtml(string html, DateTime scrapedAtUtc, out DateTime? value)
    {
        value = null;

        var anchors = anchor.Matches(html)
            .Select(m => (Start: m.Index, End: m.Index + m.Length))
            .ToList();

        foreach (Match block in paragraph.Matches(html))
        {
            if (anchors.Any(a => block.Index < a.End && block.Index + block.Length > a.Start)) continue;
            if (inner_anchor.IsMatch(block.Value)) continue;
            if (!block.Value.Contains('·')) continue;

            var text = HttpUtility.HtmlDecode(tag.Replace(block.Value, " "));
            if (TryScanAge(text, scrapedAtUtc, out value)) return true;
        }

        return false;
    }

    private static bool TryExtractRelativeText(string text, DateTime scrapedAtUtc, out DateTime? value)
    {
        foreach (var line in text.Split('\n'))
        {
            if (!line.Contains('·')) continue;
            if (TryScanAge(line, scrapedAtUtc, out value)) return true;
        }

        value = null;
        return false;
    }

    private static bool TryScanAge(string text, DateTime scrapedAtUtc, out DateTime? value)
    {
        foreach (var part in HttpUtility.HtmlDecode(text).Split('·'))
        {
            var token = part.Trim();
            if (token.Length == 0) continue;

            var age = prefixed_age.Match(token);
            if (!age.Success) age = bare_age.Match(token);

            if (age.Success)
            {
                value = Midpoint(scrapedAtUtc, long.Parse(age.Groups[1].Value), age.Groups[2].Value);
                return true;
            }            if (zero_age.IsMatch(token))
            {
                value = AsUtcWall(scrapedAtUtc);
                return true;
            }
        }

        value = null;
        return false;
    }

    private static DateTime Midpoint(DateTime scrapedAtUtc, long count, string unit)
    {
        var seconds = unit.ToLowerInvariant() switch
        {
            "minute" or "minutes" => 60.0,
            "hour" or "hours" => 3600.0,
            "day" or "days" => 86400.0,
            "week" or "weeks" => 604800.0,
            _ => 2592000.0,
        };

        return AsUtcWall(scrapedAtUtc - TimeSpan.FromSeconds(count * seconds / 2.0));
    }

    private static DateTime AsUtcWall(DateTime time)
    {
        return DateTime.SpecifyKind(time, DateTimeKind.Unspecified);
    }
}
