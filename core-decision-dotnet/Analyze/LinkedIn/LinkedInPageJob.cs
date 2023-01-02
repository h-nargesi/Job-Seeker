﻿using System.Web;
using Serilog;

namespace Photon.JobSeeker.LinkedIn
{
    class LinkedInPageJob : LinkedInPage
    {
        public override int Order => 10;

        public override TrendState TrendState => TrendState.Analyzing;

        public LinkedInPageJob(LinkedIn parent) : base(parent) { }

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
                    commands.Add(Command.Click(@"button[class=""jobs-save-button""]"));
                    commands.Add(Command.Wait(3000));
                }

                // TODO: apply link
            }

            Log.Debug(string.Join(", ", parent.Name, job.Title, job.Code, job.Log));
            database.Job.Save(job, JobFilter.Log | JobFilter.State | JobFilter.Link | JobFilter.Score);

            commands.Add(Command.Close());
            return commands.ToArray();
        }

        private Job LoadJob(Database database, string url, string content)
        {
            var url_matched = reg_job_url.Match(url);
            if (!url_matched.Success) throw new Exception($"Invalid job url ({parent.Name}).");

            var code = url_matched.Groups[1].Value;
            var job = database.Job.Fetch(parent.ID, code);
            var filter = JobFilter.Title | JobFilter.Html | JobFilter.Content | JobFilter.Tries;

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
            if (!title_match.Success)
                Log.Warning("Title not found ({0}, {1})", parent.Name, code);
            else job.Title = HttpUtility.HtmlDecode(title_match.Groups[1].Value).Trim();

            job.SetHtml(GetContent(content));

            database.Job.Save(job, filter);

            return job;
        }

        private string GetContent(string html)
        {
            var start_match = reg_job_content_start.Match(html);
            if (!start_match.Success) return html;

            var end_match = reg_job_content_end.Match(html);
            if (!end_match.Success) return html;

            return html.Substring(start_match.Index + start_match.Length, end_match.Index - start_match.Index - start_match.Length);
        }
    }
}