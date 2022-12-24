using System.Text.RegularExpressions;

class IamExpatPageJob : IamExpatPage
{
    public override int Order => 10;

    private static readonly Regex reg_job_url = new(@"/career/jobs-[^""\']+/it-technology/[^""\']+/(\d+)/?");

    public IamExpatPageJob(Agency parent) : base(parent) { }

    public override Command[]? IssueCommand(string url, string content)
    {
        if (reg_job_url.IsMatch(url)) return null;

        var job = LoadJob(url, content);

        // load options
        var options = LoadOptions();

        // eligibility
        var eligibility = EvaluateEligibility(job, options);

        // rejection : return
        if (!eligibility)
        {
            job.State = JobState.rejected;
            // TODO: save job
            return new[] { Command.Close() };
        }

        // easy apply : else return

        // apply

        return Array.Empty<Command>();
    }

    private Job LoadJob(string url, string content)
    {
        var code = reg_job_url.Match(url).Groups[1].Value;

        using var database = Database.Open();
        using var reader = database.Read(q_select_job, Parent.ID, code);

        Job job;

        if (reader.Read())
        {
            job = new Job
            {
                JobID = (long)reader["JobID"],
                Code = code,
                AgencyID = Parent.ID,
                State = Enum.Parse<JobState>((string)reader["State"]),
                Title = (string)reader["Title"],
            };
        }
        else
        {
            job = new Job
            {
                Code = code,
                AgencyID = Parent.ID,
                State = JobState.saved,
            };

            // TODO find title

            // TODO insert job
        }

        job.Url = url;
        job.Html = content;

        // TODO update job

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
        Parent.logger.LogDebug(string.Join(", ", Parent.Name, job.Title, job.Code, job.Log));

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

    private static JobOption[] LoadOptions()
    {
        using var database = Database.Open();
        using var reader = database.Read(q_select_job_options);

        var option_list = new List<JobOption>();
        while (reader.Read())
        {
            option_list.Add(new JobOption()
            {
                Title = (string)reader["Title"],
                Score = (long)reader["Score"],
                Pattern = new Regex((string)reader["Pattern"]),
                Options = (string)reader["Options"],
            });
        }

        return option_list.ToArray();
    }

    private const string q_select_job = @"
SELECT * FROM Job WHERE AgencyID = @agency and Code = @code";

    private const string q_select_job_options = @"
SELECT Score, Title, Pattern, Options FROM JobOption WHERE Efective != 0";
}