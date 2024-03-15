using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Indeed
{
    abstract class IndeedPage : Page
    {
        protected readonly Indeed parent;

        protected IndeedPage(Indeed parent) : base(parent) => this.parent = parent;

        protected static readonly Regex reg_login_but = new(@"<a[^>]+href=[""']https://secure\.indeed\.com/account/login[^""']*[""'][^>]*>Sign in</a>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_login_url = new(@"^https?://secure\.indeed\.com/auth", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_url = new(@"^https?://[^/]*indeed\.com/jobs\?", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_keywords_url = new(@$"(^|&|\?)q={Agency.SearchTitle}(&|$)", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_end = new(@"<a[^>]+aria-label=[""']Next Page[""']", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_url = new(@"/rc/clk\?jk=(\w+)", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_view = new(@"/viewjob\?jk=(\w+)", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_title = new(@"<h1[^>]*>([^<]*<span[^>]*>)?([^<]*)(</span>[^<]*)?</h1>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_adding = new(@"<a[^>]+href=[""']#[""'][^>]+rel=[""']nofollow[""'][^>]+title=[""'][^""']*Add to favourites[""'][^>]*>", RegexOptions.IgnoreCase);

        public static readonly Regex reg_job_acceptability_checker = new(@"\bThis job has expired on Indeed\b", RegexOptions.IgnoreCase);
    }
}