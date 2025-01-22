using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Stepstone
{
    class StepstonePageSearch : StepstonePage
    {
        public override int Order => 20;

        public override TrendState TrendState => TrendState.Seeking;

        public StepstonePageSearch(Stepstone parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_search_url.IsMatch(url)) return null;

            if (!reg_search_keywords_url_title.IsMatch(url) ||
                !reg_search_keywords_url_en.IsMatch(url) ||
                !reg_search_keywords_url_type.IsMatch(url))
            {
                return new Command[]
                {
                    Command.Go(parent.SearchLink),
                };
            }

            var codes = new HashSet<string>();
            using var database = Database.Open();

            foreach (Match job_match in reg_job_url.Matches(content).Cast<Match>())
            {
                var code = job_match.Groups[1].Value;

                if (codes.Contains(code)) continue;
                codes.Add(code);

                database.Job.Save(new
                {
                    AgencyID = parent.ID,
                    Url = string.Join("", parent.BaseUrl, job_match.Value),
                    Country = parent.CurrentMethodTitle,
                    Code = code,
                    State = JobState.Saved
                });
            }

            if (reg_search_end.IsMatch(content)) return Array.Empty<Command>();
            else return new Command[]
            {
                Command.Click(@"a[aria-label=""Next""]"),
                Command.Wait(3000),
                Command.Reload(),
            };
        }
    }
}