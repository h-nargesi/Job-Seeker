using System.Web;
using Serilog;

namespace Photon.JobSeeker.Pages;

public abstract class JobPage : PageBase
{
    public override int Order => 10;

    public override TrendState TrendState => TrendState.Analyzing;

    protected JobPage(Agency parent) : base(parent) { }

    public override Command[]? IssueCommand(string url, string content)
    {
        if (CheckInvalidUrl(url, content)) return null;

        var job = LoadJob(url, content);

        using var evaluator = new JobEligibilityHelper();
        var state = evaluator.EvaluateJobEligibility(job, Parent.JobAcceptabilityChecker);

        var commands = new List<Command>();

        if (state == JobState.Attention)
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

    protected abstract string GetHtmlContent(string html);

    protected virtual void ChceckJob(Job job) { }

    private Job LoadJob(string url, string html)
    {
        using var database = Database.Open();

        var code = GetJobCode(url);
        if (string.IsNullOrEmpty(code)) throw new Exception($"Invalid job url ({Parent.Name}).");

        var job = database.Job.Fetch(Parent.ID, code);
        var filter = JobFilter.Title | JobFilter.Html | JobFilter.Content;

        GetJobContent(html, out var html_code, out var apply_link, out var title);

        if (string.IsNullOrEmpty(code))
            if (!string.IsNullOrEmpty(html_code)) code = html_code;
            else throw new Exception($"Job shortlink not found ({Parent.Name}).");

        if (job != null)
        {
            var temp_job = database.Job.Fetch(Parent.ID, code);
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

                filter = JobFilter.All;
            }
        }

        if (!string.IsNullOrEmpty(apply_link))
        {
            filter |= JobFilter.Link;
            job.Link = apply_link;
        }

        if (string.IsNullOrEmpty(title))
            Log.Warning("Title not found ({0}, {1})", Parent.Name, code);
        else job.Title = HttpUtility.HtmlDecode(title).Trim();

        job.SetHtml(GetHtmlContent(html));

        ChceckJob(job);

        Log.Information("{0} Job: {1} ({2})", Parent.Name, job.Title, job.Code);
        database.Job.Save(job, filter);

        return job;
    }
}