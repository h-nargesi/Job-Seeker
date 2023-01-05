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

            if (!reg_search_keyword.IsMatch(content) ||
                !reg_search_location.IsMatch(content))
            {
                return new Command[]
                {
                    Command.Fill(@"#jobs-search-box-keyword-id-ember25", Agency.SearchTitle),
                    Command.Fill(@"#jobs-search-box-location-id-ember25]", parent.Location),
                    Command.Wait(1000),
                    Command.Click(@"button.jobs-search-box__submit-button")
                };
            }

            if (!reg_search_options.IsMatch(content))
            {
                return new Command[]
                {
                    Command.Click(@"label[for=""experience-3""]"),
                    Command.Click(@"label[for=""experience-4""]"),
                    Command.Wait(3000),
                    Command.Click(@"#ember167")
                };
            }

            var codes = new HashSet<long>();
            using var database = Database.Open();
            var base_link = parent.Link.Trim().EndsWith("/") ? parent.Link[..^1] : parent.Link;

            foreach (Match job_match in reg_job_url.Matches(content).Cast<Match>())
            {
                var code = long.Parse(job_match.Groups[1].Value);

                if (codes.Contains(code)) continue;
                codes.Add(code);

                database.Job.Save(new
                {
                    AgencyID = parent.ID,
                    Url = string.Join("", base_link, job_match.Value),
                    Code = code.ToString(),
                    State = JobState.Saved
                });
            }

            return GetNextPageButton(content);
        }

        private Command[] GetNextPageButton(string content)
        {
            var match = reg_search_page_panel.Match(content);
            if (!match.Success) return new Command[0];
            content = content.Substring(match.Index + match.Length);

            match = reg_search_page_panel_end.Match(content);
            if (!match.Success) return new Command[0];
            content = content.Substring(0, match.Index);

            match = reg_search_current_page.Match(content);
            if (!match.Success) return new Command[0];
            content = content.Substring(match.Index + match.Length);

            match = reg_search_other_page.Match(content);
            if (!match.Success) return new Command[0];

            return new Command[]
            {
                Command.Click(@$"button[aria-label=""{match.Groups[1].Value}""]"),
                Command.Recheck(),
            };
        }
    }
}