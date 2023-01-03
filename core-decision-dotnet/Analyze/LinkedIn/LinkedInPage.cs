using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn
{
    abstract class LinkedInPage : Page
    {
        protected readonly LinkedIn parent;

        protected LinkedInPage(LinkedIn parent) : base(parent) => this.parent = parent;

        protected static readonly Regex reg_login_but = new(@"<button[^>]+type=[""']submit[""'][^>]+>\s*Sign\s+in\s*</button>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_url = new(@"^https?://[^/]*linkedin\.com/jobs/search", RegexOptions.IgnoreCase);

        internal static Regex reg_search_location_url = new(@"(^|&)location=Netherlands(&|$)", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_keywords_url = new(@"(^|&)keywords=developer(&|$)", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_options_url = new(@"(^|&)f_E=3%2C4(&|$)", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_page_panel = new(@"<ul[^>]+class=[""']artdeco-pagination__pages[^""']*[""']>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_page_panel_end = new(@"</ul>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_current_page = new(@"<button[^>]+aria-current=[""']true[""'][^>]*>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_other_page = new(@"<button[^>]+aria-label=[""']([^""']+)[""'][^>]*>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_url = new(@"/jobs/view/(\d+)/", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_title = new(@"<h1[^>]*>([^<]*)</h1>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_adding = new(@"<span\s+aria-hidden=[""']true[""']>Save</span>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_apply = new(@"<span\s+aria-hidden=[""']true[""']>Save</span>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_content_start = new(@"role=[""']main[""'][^>]*>", RegexOptions.IgnoreCase);
        
        protected static readonly Regex reg_job_content_end = new(@"<div class=[""']jobs-similar-jobs[^""']*[""']>", RegexOptions.IgnoreCase);
    }
}