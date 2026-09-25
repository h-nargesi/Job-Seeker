using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat;

interface IamExpatPage
{
    protected static readonly Regex reg_login_but = new(@"<a[^>]+href=[""']/login[""']", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_login_url = new(@"^https?://[^/]*iamexpat\.[\w]{2,3}/login", RegexOptions.IgnoreCase);

    internal static Regex reg_search_url = new(@"^https?://[^/]*iamexpat\.[\w]{2,3}/career/jobs-netherlands", RegexOptions.IgnoreCase);

    internal const string search_category_path = "/it-technology-positions";

    protected static readonly Regex reg_search_category_url = new(@"/it-technology-positions([/?#]|$)", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_search_page_param = new(@"[?&]page=(\d+)", RegexOptions.IgnoreCase);

    internal static readonly Regex reg_job_url = new(@"/career/jobs-[\w-]+/it-technology-positions/([\w-]+)/([A-Za-z0-9]{8,})", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_job_title = new(@"<h1[^>]*class=""title-3""[^>]*>([^<]+)</h1>", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_job_title_fallback = new(@"<title>\s*([^<]+?)\s*</title>", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_job_apply = new(@"<a\s+href=[""'](https?://[^""']+)[""'][^>]*>\s*Apply\s+for\s+this\s+position", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_job_content_start = new(@"<div\s+class=""BodyCenter_main__[\w-]+"">", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_job_content_end = new(@"<h2[^>]*>\s*Similar\s+jobs\s*</h2>", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_job_content_end_fallback = new(@"LATEST\s+CAREER\s+NEWS", RegexOptions.IgnoreCase);

    protected static string GetJobCode(Match match)
    {
        var i = match.Groups.Count;
        var code = string.Empty;
        while (string.IsNullOrEmpty(code) && --i >= 0)
            code = match.Groups[i].Value;
        return code;
    }
}
