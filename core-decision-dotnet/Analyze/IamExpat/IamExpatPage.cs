using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat
{
    abstract class IamExpatPage : Page
    {
        protected readonly IamExpat parent;

        protected IamExpatPage(IamExpat parent) : base(parent) => this.parent = parent;

        protected static readonly Regex reg_login_but = new(@"<a[^>]+href=[""']/login[""']", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_login_url = new(@"^https?://[^/]*iamexpat\.com/login", RegexOptions.IgnoreCase);

        internal static Regex reg_search_url = new(@"^https?://[^/]*iamexpat\.nl/career/jobs-netherlands", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_title = new(@"<h1>[^<]*IT[^<]*Technology[^<]*</h1>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_end = new(@"<a[^>]+title=[""']Go to next page[""']", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_url = new(@"/career/jobs-[\w-]+(/[\w-]+)*/it-technology/([\w-]+)(/(\d+))?", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_shortlink = new(@"<link\s+rel=[""']shortlink[""'] href=[""']/node/(\d+)[""']>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_title = new(@"<h1[^>]+class=[""'][^""']*article__title[^""']*[""'][^>]*>([^<]*)</h1>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_apply = new(@"<a[^>]+href=[""']([^""']+)[""'][^>]*>Apply\s+Now</a>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_adding = new(@"<a[^>]+href=[""']#[""'][^>]+rel=[""']nofollow[""'][^>]+title=[""'][^""']*Add to favourites[""'][^>]*>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_content_start = new(@"<article[^>]*>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_content_end = new(@"</article>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_content_apply_start = new(@"<form[^>]*>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_content_apply_end = new(@"</form>", RegexOptions.IgnoreCase);

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