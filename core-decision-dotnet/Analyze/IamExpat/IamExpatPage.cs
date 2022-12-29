using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat
{
    abstract class IamExpatPage : Page
    {
        protected readonly IamExpat parent;

        protected IamExpatPage(IamExpat parent) : base(parent) => this.parent = parent;

        protected static readonly Regex reg_login_but = new(@"<a[^>]+href=[""']/login[""']");

        protected static readonly Regex reg_login_url = new(@"iamexpat\.com/login");

        protected static readonly Regex reg_search_url = new(@"://[^/]*iamexpat\.nl/career/jobs-netherlands");

        protected static readonly Regex reg_search_title = new(@"<h1>[^<]*IT[^<]*Technology[^<]*</h1>");

        protected static readonly Regex reg_job_url = new(@"/career/jobs-[\w-]+(/[\w-]+)*/it-technology/([\w-]+)(/(\d+))?");

        protected static readonly Regex reg_job_shortlink = new(@"<link\s+rel=[""']shortlink[""'] href=[""']/node/(\d+)[""']>");

        protected static readonly Regex reg_job_title = new(@"<h1[^>]+class=[""']article__title[""'][^>]*>([^<]*)</h1>");

        protected static readonly Regex reg_job_apply = new(@"<a[^>]+href=[""']([^""']+)[""'][^>]*>Apply\s+Now</a>");

        protected static readonly Regex reg_job_adding = new(@"<a[^>]+href=[""']#[""'][^>]+rel=[""']nofollow[""'][^>]+title=[""'][^""']*Add to favourites[""'][^>]*>");

        protected static readonly Regex reg_job_content_start = new(@"<article[^>]*>");

        protected static readonly Regex reg_job_content_end = new(@"</article>");

        protected static string GetJobCode(Match match)
        {
            var i = match.Groups.Count - 1;
            var code = "";
            while (string.IsNullOrEmpty(code) && --i >= 0)
                code = match.Groups[i].Value;
            return code;
        }
    }
}