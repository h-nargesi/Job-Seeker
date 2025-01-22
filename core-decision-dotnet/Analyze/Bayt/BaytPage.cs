using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Bayt;

interface BaytPage
{
    protected static readonly Regex reg_login_but = new(@"<a[^>]+href=[""']https://[^/]*bayt\.com/account/login[^""']*[""'][^>]*>Sign in</a>", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_login_url = new(@"^https?://[^/]*bayt\.com/en/login/", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_search_url = new(@"^https?://[^/]*bayt\.com/jobs\?", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_search_keywords_url = new(@$"/{Agency.SearchTitle}/", RegexOptions.IgnoreCase);

    internal static Regex reg_search_location_url = new(@"en/oman/jobs", RegexOptions.IgnoreCase);

     protected static readonly Regex reg_search_next = new(@"<a[^>]+rel=[""']nofollow[""'][^>]+href=[""']([^""']+)[""'][^>]+>", RegexOptions.IgnoreCase);

     protected static readonly Regex reg_job_url = new(@"href=[""'](/en/\w+/jobs/[\w-]+(\d+)/)[""']", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_job_view = new(@"-(\d+)/^", RegexOptions.IgnoreCase);

    protected static readonly Regex reg_job_title = new(@"<h1[^>]+id=[""']job_title[""'][^>]*>([^<]+)</h1>", RegexOptions.IgnoreCase);

    public static readonly Regex reg_job_acceptability_checker = new(@"\bExpired or no longer accepting applications\b", RegexOptions.IgnoreCase);
}
