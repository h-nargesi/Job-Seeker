using System.Text.RegularExpressions;
using Serilog;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpatPageJob : IamExpatPage
    {
        public override int Order => 10;

        public override TrendType TrendType => TrendType.Analyzing;

        private static readonly Regex reg_job_url = new(@"/career/jobs-[^""\']+/it-technology/[^""\']+/(\d+)/?");
        private static readonly Regex reg_job_title = new(@"<h1[^>]+class=""article__title""[^>]*>([^<]*)</h1>");
        private static readonly Regex reg_job_apply = new(@"<a[^>]+href=""([^""']+)""[^>]*>Apply\s+Now</a>");

        public IamExpatPageJob(IamExpat parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_job_url.IsMatch(url)) return null;

            using var database = Database.Open();

            var job = LoadJob(database, url, content);

            var options = database.JobOption.FetchAll();

            var eligibility = EvaluateEligibility(job, options);

            if (!eligibility) job.State = JobState.rejected;
            else
            {
                job.State = JobState.attention;

                var apply_match = reg_job_apply.Match(content);
                if (apply_match == null) job.Log += "|Apply button not found!";
                else
                {
                    job.Link = apply_match.Groups[1].Value;
                    if (job.Link.StartsWith('#'))
                    {
                        // TODO: easy apply
                    }
                }
            }

            Log.Debug(string.Join(", ", parent.Name, job.Title, job.Code, job.Log));
            database.Job.Save(job, JobFilter.Log | JobFilter.Title | JobFilter.Link);

            return Command.JustClose();
        }

        private Job LoadJob(Database database, string url, string content)
        {
            var code = reg_job_url.Match(url).Groups[1].Value;
            var job = database.Job.Fetch(parent.ID, code);
            var filter = JobFilter.Title | JobFilter.Html;

            if (job == null)
            {
                job = new Job
                {
                    AgencyID = parent.ID,
                    Code = code,
                    State = JobState.saved,
                    Url = reg_job_url.Match(url).Value,
                };

                filter = JobFilter.All;
            }

            var title_match = reg_job_title.Match(content);
            if (title_match == null)
                Log.Warning("Title not found ({0}, {1})", parent.Name, code);
            else job.Title = title_match.Groups[1].Value.Trim();

            job.Html = content;

            database.Job.Save(job, filter);

            return job;
        }

        private static bool EvaluateEligibility(Job job, JobOption[] options)
        {
            var logs = new List<string>(options.Length);
            var hasField = false;
            var score = 0L;

            foreach (var option in options)
            {
                var option_score = CheckOptionIn(job, option);

                logs.Add((option_score > 0 ? "+" : "-") + option.ToString());

                if (!hasField && option_score > 0 && option.Options == "field")
                {
                    hasField = true;
                }

                score += option_score;
            }

            job.Log = string.Join("|", logs);

            if (!hasField) return false;
            else return score >= MinEligibilityScore;
        }

        private static long CheckOptionIn(Job job, JobOption option)
        {
            if (!option.Pattern.IsMatch(job.Html ?? "")) return 0;

            if (option.Options.StartsWith("salary"))
            {
                var group = int.Parse(option.Options.Split("-")[2]);
                var salary = long.Parse(option.Pattern.Match(job.Html ?? "").Groups[group].Value);
                return salary * option.Score;
            }
            else return option.Score;
        }

    }
}