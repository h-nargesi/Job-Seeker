using Photon.JobSeeker.Analyze;
using Serilog;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpatPageJob : IamExpatPage
    {
        public override int Order => 10;

        public override TrendState TrendState => TrendState.Analyzing;

        public IamExpatPageJob(IamExpat parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_job_url.IsMatch(url)) return null;

            using var database = Database.Open();

            var job = LoadJob(database, url, content);

            var eligibility = JobEligibilityHelper.EvaluateJobEligibility(database, job);

            var commands = new List<Command>();

            if (!eligibility) job.State = JobState.rejected;
            else
            {
                job.State = JobState.attention;

                if (reg_job_adding.IsMatch(content))
                {
                    commands.Add(Command.Click(@"a[rel=""nofollow""]"));
                    commands.Add(Command.Wait(3000));
                }

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
            database.Job.Save(job, JobFilter.Log | JobFilter.State | JobFilter.Link | JobFilter.Score);

            commands.Add(Command.Close());
            return commands.ToArray();
        }

        private Job LoadJob(Database database, string url, string content)
        {
            var url_matched = reg_job_url.Match(url);
            var code = url_matched.Groups[1].Value;
            var job = database.Job.Fetch(parent.ID, code);
            var filter = JobFilter.Title | JobFilter.Html;

            if (job == null)
            {
                job = new Job
                {
                    AgencyID = parent.ID,
                    Code = code,
                    State = JobState.saved,
                    Url = url_matched.Value,
                };

                filter = JobFilter.All;
            }

            var title_match = reg_job_title.Match(content);
            if (title_match == null)
                Log.Warning("Title not found ({0}, {1})", parent.Name, code);
            else job.Title = title_match.Groups[1].Value.Trim();

            job.Html = GetContent(content);

            database.Job.Save(job, filter);

            return job;
        }

        private string GetContent(string html)
        {
            var start_match = reg_job_content_start.Match(html);
            if (start_match == null) return html;

            var end_match = reg_job_content_end.Match(html);
            if (end_match == null) return html;

            return html.Substring(start_match.Index, end_match.Index + end_match.Length);
        }
    }
}