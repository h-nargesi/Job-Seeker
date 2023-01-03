using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed
{
    abstract class IndeedPage : Page
    {
        protected readonly Indeed parent;

        protected IndeedPage(Indeed parent) : base(parent) => this.parent = parent;

        protected static readonly Regex reg_login_but = new(@"<a[^>]+href=[""']https://secure\.indeed\.com/account/login[^""']*[""'][^>]*>Sign in</a>", RegexOptions.IgnoreCase); //

        protected static readonly Regex reg_login_url = new(@"indeed\.com/account/login", RegexOptions.IgnoreCase); //

        protected static readonly Regex reg_search_url = new(@"://[^/]*indeed\.com/jobs\?", RegexOptions.IgnoreCase); // 

        protected static readonly Regex reg_search_keywords_url = new(@"(^|&)q=developer(&|$)", RegexOptions.IgnoreCase); //

        protected static readonly Regex reg_search_end = new(@"<a[^>]+aria-label=[""']Next Page[""']", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_url = new(@"/rc/clk?jk=([0-9a-f]+)", RegexOptions.IgnoreCase); // 

        protected static readonly Regex reg_job_view = new(@"/viewjob?jk=([0-9a-f]+)", RegexOptions.IgnoreCase); // 

        protected static readonly Regex reg_job_title = new(@"<h1[^>]*>([^<]*)</h1>", RegexOptions.IgnoreCase); //

        protected static readonly Regex reg_job_apply = new(@"<a[^>]+href=[""']([^""']+)[""'][^>]*>Apply\s+Now</a>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_adding = new(@"<a[^>]+href=[""']#[""'][^>]+rel=[""']nofollow[""'][^>]+title=[""'][^""']*Add to favourites[""'][^>]*>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_content_start = new(@"<article[^>]*>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_content_end = new(@"</article>", RegexOptions.IgnoreCase);

        protected static string GetJobCode(Match match)
        {
            var i = match.Groups.Count;
            var code = "";
            while (string.IsNullOrEmpty(code) && --i >= 0)
                code = match.Groups[i].Value;
            return code;
        }
    }
}