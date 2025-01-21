using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Bayt;

interface BaytPage
{
    protected static readonly Regex reg_login_but = new(@"<a[^>]+href=[""']https://[\w\.]*bayt\.com/account/login[^""']*[""'][^>]*>Sign in</a>", RegexOptions.IgnoreCase);

    // protected static readonly Regex reg_login_url = new(@"^https?://secure\.indeed\.com/auth", RegexOptions.IgnoreCase);

    // protected static readonly Regex reg_search_url = new(@"^https?://[^/]*indeed\.com/jobs\?", RegexOptions.IgnoreCase);

    // protected static readonly Regex reg_search_keywords_url = new(@$"(^|&|\?)q={Agency.SearchTitle}(&|$)", RegexOptions.IgnoreCase);

    internal static Regex reg_search_location_url = new(@"en/oman/jobs", RegexOptions.IgnoreCase);

    // protected static readonly Regex reg_search_end = new(@"<a[^>]+aria-label=[""']Next Page[""']", RegexOptions.IgnoreCase);

    // protected static readonly Regex reg_job_url = new(@"/rc/clk\?jk=(\w+)", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_job_view = new(@"-(\d+)/^", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_job_title = new(@"<h1[^>]*>([^<]*<span[^>]*>)?([^<]*)(</span>[^<]*)?</h1>", RegexOptions.IgnoreCase);

    public static readonly Regex reg_job_acceptability_checker = new(@"\bExpired or no longer accepting applications\b", RegexOptions.IgnoreCase);
}
