using System.Web;
using HtmlAgilityPack;
using Serilog;

namespace Photon.JobSeeker.Stepstone
{
    class StepstonePageJob : StepstonePage
    {
        public override int Order => 10;

        public StepstonePageJob(Stepstone parent) : base(parent) { }

        public override TrendState TrendState => TrendState.Analyzing;

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_job_url.IsMatch(url)) return null;

            var job = LoadJob(url, content);

            if (job.State == JobState.NotApprovedRegex) return [];

            using var evaluator = new JobEligibilityHelper(Parent.DatabaseFactory);
            var state = evaluator.EvaluateJobEligibility(job, Parent.JobAcceptabilityChecker);

            var commands = new List<Command>();

            if (state == JobState.AiPending)
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
        using var database = parent.DatabaseFactory.Open();

        var url_matched = reg_job_url.Match(url);
        if (!url_matched.Success) throw new Exception($"Invalid job url ({parent.Name}).");

        var code = url_matched.Groups[1].Value;
        var job = database.Job.Fetch(parent.ID, code);
        var new_job = job == null;

        if (new_job)
        {
            job = new Job
            {
                AgencyID = parent.ID,
                Code = code,
                State = JobState.Saved,
                Url = string.Join("", parent.BaseUrl, url_matched.Value),
            };
        }

        var title_match = reg_job_title.Match(html);
        if (!title_match.Success)
            Log.Warning("Title not found ({0}, {1})", parent.Name, code);
        else job!.Title = HttpUtility.HtmlDecode(title_match.Groups[1].Value).Trim();

        var html_content = GetHtmlContent(html);
        var incoming_text = JobContent.GetTextContent(html_content);
        var content_changed = JobContent.HasChanged(job!.Content, incoming_text);
        job.Html = html_content;
        job.Content = incoming_text;

        Log.Information("{0} Job: {1} ({2})", parent.Name, job.Title, job.Code);
        if (content_changed)
            Log.Information("{0} Job content changed: {1}", parent.Name, job.Code);

        if (new_job) database.Job.InsertJob(job);
        else database.Job.UpdateStepstoneJob(job);

        return job;
    }

        public static string GetHtmlContent(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var main_content = doc.DocumentNode.SelectNodes("//div[contains(@class,'reb-main')]")?
                                               .FirstOrDefault();

            return main_content?.OuterHtml ?? html;
        }
    }
}