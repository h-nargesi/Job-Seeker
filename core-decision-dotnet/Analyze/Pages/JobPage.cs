using System.Web;
using Serilog;

namespace Photon.JobSeeker.Pages;

public abstract class JobPage(Agency parent) : PageBase(parent)
{
    public override int Order => 10;

    public override TrendState TrendState => TrendState.Analyzing;

    public override Command[]? IssueCommand(string url, string content)
    {
        if (CheckInvalidUrl(url, content)) return null;

        using var database = Parent.DatabaseFactory.Open();
        var job = LoadJob(url, content, database);

        if (job.State == JobState.NotApprovedRegex) return [];

        using var evaluator = new JobEligibilityHelper(database);
        var state = evaluator.EvaluateJobEligibility(job, Parent.JobAcceptabilityChecker);

        var commands = new List<Command>();

        if (state == JobState.AiPending)
        {
            var fallow = JobFallow(content);
            if (fallow?.Length > 0)
            {
                commands.AddRange(fallow);
            }

            // TODO: apply link
        }

        return commands.ToArray();
    }

    protected abstract string GetJobCode(string url);

    protected abstract Command[]? JobFallow(string content);

    protected abstract void GetJobContent(string html, out string? code, out string? apply, out string? title);

    public abstract string GetHtmlContent(string html);

    protected virtual void ChceckJob(Job job) { }

    private Job LoadJob(string url, string html, Database database)
    {
        var code = GetJobCode(url);
        if (string.IsNullOrEmpty(code)) throw new Exception($"Invalid job url ({Parent.Name}).");

        var job = database.Job.Fetch(Parent.ID, code);
        var code_changed = false;

        GetJobContent(html, out var real_code, out var apply_link, out var title);

        if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(real_code))
            throw new Exception($"Job shortlink not found ({Parent.Name}).");

        if (!string.IsNullOrEmpty(real_code))
        {
            code = real_code;

            if (job != null)
            {
                var temp_job = database.Job.Fetch(Parent.ID, code);
                if (temp_job == null)
                {
                    job.Code = code;
                    code_changed = true;
                }
                else
                {
                    database.Job.Delete(job.JobID);
                    job = temp_job;
                }
            }
        }

        var new_job = false;

        if (job == null)
        {
            job = database.Job.Fetch(Parent.ID, code);

            if (job == null)
            {
                job = new Job
                {
                    AgencyID = Parent.ID,
                    Code = code,
                    State = JobState.Saved,
                    Url = url,
                };

                new_job = true;
            }
        }

        job.Country = Parent.CurrentMethod.Title;

        var link_found = false;

        if (!string.IsNullOrEmpty(apply_link))
        {
            link_found = true;
            job.Link = apply_link;
        }

        if (string.IsNullOrEmpty(title))
            Log.Warning("Title not found ({0}, {1})", Parent.Name, code);
        else job.Title = HttpUtility.HtmlDecode(title).Trim();

        var html_content = GetHtmlContent(html);
        var incoming_text = JobContent.GetTextContent(html_content);
        if (string.IsNullOrWhiteSpace(incoming_text))
            Log.Error("Job content is empty ({0}, {1})", Parent.Name, code);
        var content_changed = JobContent.HasChanged(job.Content, incoming_text);
        job.Html = html_content;
        job.Content = incoming_text;

        ChceckJob(job);

        var include_state = job.State == JobState.NotApprovedRegex;

        Log.Information("{0} Job: {1} ({2})", Parent.Name, job.Title, job.Code);
        if (content_changed)
            Log.Information("{0} Job content changed: {1}", Parent.Name, job.Code);

        if (new_job) database.Job.InsertJob(job);
        else database.Job.UpdateScrapedJob(job, code_changed, link_found, include_state);

        return job;
    }
}