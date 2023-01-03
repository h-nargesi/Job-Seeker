using System.Text.RegularExpressions;
using Photon.JobSeeker.Analyze.Models;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpatPageSearch : IamExpatPage
    {
        public override int Order => 20;

        public override TrendState TrendState => TrendState.Seeking;

        public IamExpatPageSearch(IamExpat parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_search_url.IsMatch(url)) return null;

            if (!reg_search_title.IsMatch(content))
            {
                return new Command[]
                {
                    Command.Click(@"label[for=""industry-260""]"), // it-technology
                    Command.Click(@"label[for=""ccareer-level-19926""]"), // entry-level
                    Command.Click(@"label[for=""career-level-19928""]"), // experienced
                    Command.Click(@"label[for=""contract-19934""]"),
                    Command.Wait(3000),
                    Command.Click(@"input[type=""submit""][value=""Search""]"),
                };
            }

            var codes = new HashSet<string>();
            using var database = Database.Open();
            var base_link = parent.Link.Trim().EndsWith("/") ? parent.Link[..^1] : parent.Link;

            var job_matches = reg_job_url.Matches(content).Cast<Match>();
            foreach (Match job_match in job_matches)
            {
                var code = GetJobCode(job_match);

                if (string.IsNullOrEmpty(code) || codes.Contains(code)) continue;
                codes.Add(code);

                database.Job.Save(new
                {
                    AgencyID = parent.ID,
                    Url = string.Join("", base_link, job_match.Value),
                    Code = code,
                    State = JobState.saved
                });
            }

            if (!reg_search_end.IsMatch(content)) return Command.JustClose();
            else return new Command[] { Command.Click(@"a[title=""Go to next page""]") };
        }
    }
}