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

            if (!reg_search_keywords_url.IsMatch(url) ||
                !reg_search_location_url.IsMatch(url) ||
                !reg_search_options_url.IsMatch(url))
            {
                return new Command[]
                {
                    Command.Go(@"/jobs/search/?f_E=3%2C4&geoId=102890719&keywords=developer&location=Netherlands&refresh=true"),
                };
            }

            var codes = new HashSet<long>();
            using var database = Database.Open();

            foreach (Match job in reg_job_url.Matches(content).Cast<Match>())
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

            return GetNextPageButton(content);
        }

        private Command[] GetNextPageButton(string content)
        {
            var match = reg_search_page_panel.Match(content);
            if (!match.Success) return Command.JustClose();
            content = content.Substring(match.Index + match.Length);

            match = reg_search_page_panel_end.Match(content);
            if (!match.Success) return Command.JustClose();
            content = content.Substring(0, match.Index);

            match = reg_search_current_page.Match(content);
            if (!match.Success) return Command.JustClose();
            content = content.Substring(match.Index + match.Length);

            match = reg_search_other_page.Match(content);
            if (!match.Success) return Command.JustClose();

            return new Command[] { Command.Click(@$"button[aria-label=""{match.Groups[1].Value}""]") };
        }
    }
}