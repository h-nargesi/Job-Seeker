using System.Web;
using HtmlAgilityPack;
using Serilog;

namespace Photon.JobSeeker.Bayt;

class BaytPageJob : BaytPage
{
    public override int Order => 10;

    public BaytPageJob(Bayt parent) : base(parent) { }

    public override TrendState TrendState => TrendState.Analyzing;

    public override Command[]? IssueCommand(string url, string content)
    {
        if (!reg_job_view.IsMatch(url)) return null;

        var job = LoadJob(url, content);

        using var evaluator = new JobEligibilityHelper();
        var state = evaluator.EvaluateJobEligibility(job, Parent.JobAcceptabilityChecker);

        var commands = new List<Command>();

        if (state == JobState.Attention)
        {
            // TODO: save link

            // TODO: apply link
        }

        return commands.ToArray();
    }

    private Job LoadJob(string url, string html)
    {
        using var database = Database.Open();

        var url_matched = reg_job_view.Match(url);
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
        else job.Title = HttpUtility.HtmlDecode(title_match.Groups[2].Value).Trim();

        job.SetHtml(GetHtmlContent(html));

        Log.Information("{0} Job: {1} ({2})", parent.Name, job.Title, job.Code);
        database.Job.Save(job, filter);

        if (job.Content?.Contains("Bayt does not provide services in your region") == true)
            throw new Exception("Bayt does not provide services in your region.");

        return job;
    }

    public static string GetHtmlContent(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var main_content = doc.DocumentNode.SelectNodes("//div[contains(@class,'jobsearch-ViewJobLayout-jobDisplay')]")?
                                            .FirstOrDefault();

        return main_content?.OuterHtml ?? html;
    }
}
