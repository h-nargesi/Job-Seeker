using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn
{
    abstract class LinkedInPage : Page
    {
        protected readonly LinkedIn parent;

        protected LinkedInPage(LinkedIn parent) : base(parent) => this.parent = parent;

        protected static readonly Regex reg_login_butl = new(@"<button[^>]+type=[""']submit[""'][^>]+>\s*Sign\s+in\s*</button>");

        protected static readonly Regex reg_search_url = new(@"://[^/]*linkedin\.com/jobs/search");

        protected static readonly Regex reg_search_keywords_url = new(@"f_E=3%2C4&geoId=102890719&keywords=developer&location=Netherlands&refresh=true");

        protected static readonly Regex reg_job_url = new(@"/jobs/view/(\d+)/");

        protected static readonly Regex reg_job_title = new(@"<h1[^>]*>([^<]*)</h1>");

        protected static readonly Regex reg_job_adding = new(@"<span\s+aria-hidden=[""']true[""']>Save</span>");

        protected static readonly Regex reg_job_apply = new(@"<span\s+aria-hidden=[""']true[""']>Save</span>");

        protected static readonly Regex reg_job_content_start = new(@"role=[""']main[""'][^>]*>");
        
        protected static readonly Regex reg_job_content_end = new(@"<div class=[""']jobs-similar-jobs[^""']*[""']>");
    }
}