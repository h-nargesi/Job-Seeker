using System.Text.RegularExpressions;

namespace Photon.JobSeeker.LinkedIn
{
    class LinkedInPageSearch : LinkedInPage
    {
        public override int Order => 20;

        public override TrendState TrendState => TrendState.Seeking;

        public LinkedInPageSearch(LinkedIn parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_search_url.IsMatch(url)) return null;

            if (!reg_search_keywords_url.IsMatch(url))
            {
                return new Command[]
                {
                    Command.Go(@"/jobs/search/?f_E=3%2C4&geoId=102890719&keywords=developer&location=Netherlands&refresh=true"),
                };
            }

            var codes = new HashSet<long>();
            using var database = Database.Open();

            foreach (Match job in reg_job_link.Matches(content).Cast<Match>())
            {
                var code = long.Parse(job.Groups[1].Value);

                if (codes.Contains(code)) continue;
                codes.Add(code);

                database.Job.Save(new
                {
                    AgencyID = parent.ID,
                    Url = url,
                    Code = code.ToString(),
                    State = JobState.saved
                });
            }

            return new Command[] { Command.Click(@"button[aria-current=""true""]@next") };
        }
    }
}