using System.Text.RegularExpressions;

class IamExpatPageJob : IamExpatPage
{
    public override int Order => 10;

    private static readonly Regex reg_job_url = new(@"/career/jobs-[^""\']+/it-technology/[^""\']+/(\d+)/?");
    private static readonly Regex reg_job_title = new(@"<h1[^>]+class=""article__title""[^>]*>([^<]*)</h1>");
    private static readonly Regex reg_job_internal_apply = new(@"<h1[^>]+class=""article__title""[^>]*>([^<]*)</h1>");
    private static readonly Regex reg_job_external_apply = new(@"<a[^>]+href=""([^""']+)""[^>]*>Apply Now</a>");

    public IamExpatPageJob(IamExpat parent) : base(parent) { }

    public override Command[]? IssueCommand(string url, string content)
    {
        if (reg_job_url.IsMatch(url)) return null;

        using var database = Database.Open();

        var job = LoadJob(database, url, content);

        var options = database.JobOption.FetchAll();

        var eligibility = EvaluateEligibility(job, options);

        if (!eligibility)
        {
            job.State = JobState.rejected;
            database.Job.Save(job, JobFilter.Log | JobFilter.Title);
            return new[] { Command.Close() };
        }

        var external_apply_match = reg_job_external_apply.Match(content);
        if (external_apply_match != null)
        {
            job.Link = external_apply_match.Groups[1].Value;
            job.State = JobState.attention;
            database.Job.Save(job, JobFilter.Log | JobFilter.Title | JobFilter.Link);
            return new[] { Command.Close() };
        }

        if (reg_job_internal_apply.IsMatch(content))
        {
            job.State = JobState.attention;
            database.Job.Save(job, JobFilter.Log | JobFilter.Title);
            return new[] { Command.Close() };
        }

        return Array.Empty<Command>();
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
            parent.logger.LogWarning("Title not found ({0}, {1})", parent.Name, code);
        else job.Title = title_match.Groups[1].Value.Trim();

        job.Html = content;

        database.Job.Save(job, filter);

        return job;
    }

    private bool EvaluateEligibility(Job job, JobOption[] options)
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
        parent.logger.LogDebug(string.Join(", ", parent.Name, job.Title, job.Code, job.Log));

        if (!hasField) return false;
        else return score >= MinEligibilityScore;
    }

    private long CheckOptionIn(Job job, JobOption option)
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