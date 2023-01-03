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

        protected static readonly Regex reg_search_keywords_url = new(@"(^|&)q=developer(&|$)", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_end = new(@"<a[^>]+aria-label=[""']Next Page[""']", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_url = new(@"/rc/clk?jk=([0-9a-f]+)", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_view = new(@"/viewjob?jk=([0-9a-f]+)", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_title = new(@"<h1[^>]*>([^<]*)</h1>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_adding = new(@"<a[^>]+href=[""']#[""'][^>]+rel=[""']nofollow[""'][^>]+title=[""'][^""']*Add to favourites[""'][^>]*>", RegexOptions.IgnoreCase);

    }
}