using System.Text.RegularExpressions;

/*
https://www.stepstone.de/work/full-time/developer?ct=222&fdl=en
https://www.stepstone.de/candidate/login?login_source=Homepage_top-login&intcid=Button_Homepage-navigation_login
https://www.stepstone.de/candidate/login
https://www.stepstone.de/jobs--Data-Scientist-m-f-d-Machine-Learning-InsurTech-Frankfurt-am-Main-CHECK24--9906753-inline.html?rltr=396_21_25_seorl_m_0_0_1_0_0_0
https://www.stepstone.de/work/full-time/developer?page=2&ct=222&fdl=en
*/

namespace Photon.JobSeeker.Stepstone
{
    abstract class StepstonePage : Page
    {
        protected readonly Stepstone parent;

        protected StepstonePage(Stepstone parent) : base(parent) => this.parent = parent;

        protected static readonly Regex reg_login_but = new(@"<div[^>]+>Sign\s+in</div>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_login_profile = new(@"^https?://[^/]*stepstone\.de/profile", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_login_url = new(@"^https?://[^/]*stepstone\.de/candidate/login", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_url = new(@"^https?://[^/]*stepstone\.de/work", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_keywords_url_title = new(@$"/work/full-time/{Agency.SearchTitle}\?", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_keywords_url_en = new(@"(^|&|\?)fdl=en(&|$)", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_keywords_url_type = new(@"(^|&|\?)ct=222(&|$)", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_search_end = new(@"<button[^>]+aria-label=[""']Next[""']", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_url = new(@"/jobs[^""']+(\d+)-inline\.html[^""']*", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_title = new(@"<span[^>]+data-at=[""']header-job-title[""'][^>]*>([^<]*)</span>", RegexOptions.IgnoreCase);

        protected static readonly Regex reg_job_adding = new(@"<a[^>]+href=[""']#[""'][^>]+rel=[""']nofollow[""'][^>]+title=[""'][^""']*Add to favourites[""'][^>]*>", RegexOptions.IgnoreCase);
    }
}