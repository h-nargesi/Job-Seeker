﻿using System.Web;
using HtmlAgilityPack;
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

            var job = LoadJob(url, content);

            using var evaluator = new JobEligibilityHelper();
            var state = evaluator.EvaluateJobEligibility(job, Parent.JobAcceptabilityChecker);

            var commands = new List<Command>();

            if (state == JobState.Attention)
            {
                if (reg_job_adding.IsMatch(content))
                {
                    commands.Add(Command.Click(@"button.jobs-save-button"));
                    commands.Add(Command.Wait(3000));
                }

                // TODO: apply link
            }

            return commands.ToArray();
        }

        private Job LoadJob(string url, string html)
        {
            using var database = Database.Open();

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
                    State = JobState.Saved,
                    Url = url_matched.Value,
                };

                filter = JobFilter.All;
            }

            var title_match = reg_job_title.Match(html);
            if (!title_match.Success)
                Log.Warning("Title not found ({0}, {1})", parent.Name, code);
            else job.Title = HttpUtility.HtmlDecode(title_match.Groups[1].Value).Trim();

            job.SetHtml(GetHtmlContent(html));

            Log.Information("{0} Job: {1} ({2})", parent.Name, job.Title, job.Code);
            database.Job.Save(job, filter);

            return job;
        }

        public static string GetHtmlContent(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var main_content = doc.DocumentNode.SelectNodes("//article")?
                                               .FirstOrDefault();

            if (main_content == null) return html;

            var title_content = doc.DocumentNode.SelectNodes("//div[contains(@class,'jobs-unified-top-card')]")?
                                                .FirstOrDefault();

            return string.Join("\n", title_content?.OuterHtml, main_content.OuterHtml);
        }
    }
}