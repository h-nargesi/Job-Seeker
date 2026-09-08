using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed;

public static class IndeedSerp
{
    public static readonly Regex reg_job_url =
        new(@"(?:/rc/clk\?jk=|/(?:m/)?viewjob\?jk=|data-jk="")(\w+)", RegexOptions.IgnoreCase);

    public static IEnumerable<string> ExtractJobCodes(string html)
    {
        var codes = new HashSet<string>();

        foreach (Match match in reg_job_url.Matches(html))
        {
            var code = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(code)) codes.Add(code);
        }

        return codes;
    }
}
