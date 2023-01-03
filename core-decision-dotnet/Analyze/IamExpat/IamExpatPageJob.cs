using System.Web;
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

            var job = LoadJob(url, content);

            using var evaluator = new JobEligibilityHelper();
            var state = evaluator.EvaluateJobEligibility(job);

            var commands = new List<Command>();

            if (state == JobState.attention)
            {
                if (reg_job_adding.IsMatch(content))
                {
                    commands.Add(Command.Click(@"a[rel=""nofollow""]"));
                    commands.Add(Command.Wait(3000));
                }
                
                if (job.Link?.StartsWith("#") == true)
                {
                    // TODO: easy apply
                }
            }

            commands.Add(Command.Close());
            return commands.ToArray();
        }

        private Job LoadJob(string url, string html)
        {
            using var database = Database.Open();

            var url_matched = reg_job_url.Match(url);
            if (!url_matched.Success) throw new Exception($"Invalid job url ({parent.Name}).");

            var code = GetJobCode(url_matched);
            var job = database.Job.Fetch(parent.ID, code);

            var filter = JobFilter.Title | JobFilter.Html | JobFilter.Content | JobFilter.Link | JobFilter.Tries;

            var code_matched = reg_job_shortlink.Match(html);
            if (!code_matched.Success) throw new Exception($"Job shortlink not found ({parent.Name}).");
            code = code_matched.Groups[1].Value;
            if (job != null)
            {
                var temp_job = database.Job.Fetch(parent.ID, code);
                if (temp_job == null)
                {
                    job.Code = code;
                    filter |= JobFilter.Code;
                }
                else
                {
                    database.Job.Delete(job.JobID);
                    job = temp_job;
                }
            }
            else
            {
                job = database.Job.Fetch(parent.ID, code);

                if (job == null)
                {
                    var base_link = parent.Link.Trim().EndsWith("/") ? parent.Link[..^1] : parent.Link;

                    job = new Job
                    {
                        AgencyID = parent.ID,
                        Code = code,
                        State = JobState.saved,
                        Url = string.Join("", base_link, url_matched.Value),
                    };

                    filter = JobFilter.All;
                }
            }

            var apply_match = reg_job_apply.Match(html);
            if (apply_match.Success) job.Link = apply_match.Groups[1].Value;
            
            var title_match = reg_job_title.Match(html);
            if (!title_match.Success)
                Log.Warning("Title not found ({0}, {1})", parent.Name, code);
            else job.Title = HttpUtility.HtmlDecode(title_match.Groups[1].Value).Trim();

            job.SetHtml(GetHtmlContent(html));

            Log.Information("{0} Job: {1} ({2})", parent.Name, job.Title, job.Code);
            database.Job.Save(job, filter);

            return job;
        }

        private string GetHtmlContent(string html)
        {
            var start_match = reg_job_content_start.Match(html);
            if (!start_match.Success) return html;

            var end_match = reg_job_content_end.Match(html);
            if (!end_match.Success) return html;

            return html.Substring(start_match.Index, end_match.Index + end_match.Length - start_match.Index);
        }
    }
}