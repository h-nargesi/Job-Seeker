﻿using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Serilog;

namespace Photon.JobSeeker;

public class JobEligibilityHelper : IDisposable
{
    private readonly static Regex remove_new_lines = new(@"(?<=\n)[\n\s]+");
    private readonly static Regex words = new(@"[a-zA-Z]{3,}");
    private readonly static HashSet<string> invalid_tag =
    [
        "script", "head", "style"
    ];
    public const long MinEligibilityScore = 100;

    private readonly Dictionaries dictionaries;
    private readonly Database database;
    private readonly JobOption[] options;

    private static readonly object revaluation_lock = new();
    public static RevaluationProcess? CurrentRevaluationProcess { get; private set; }

    public JobEligibilityHelper()
    {
        dictionaries = Dictionaries.Open();
        database = Database.Open();
        options = database.JobOption.FetchAll();
    }

    public static Task RunRevaluateProcess(Analyzer analyzer)
    {
        lock (revaluation_lock)
        {
            if (CurrentRevaluationProcess == null)
            {
                using var database = Database.Open();
                var start_time = DateTime.Now.AddSeconds(-1);
                var total_count = (int)database.Job.FetchFromCount(start_time);
                CurrentRevaluationProcess = new RevaluationProcess(start_time, total_count);
            }

            CurrentRevaluationProcess.ProcessCount++;
        }

        return Task.Run(() =>
        {
            using var evaluator = new JobEligibilityHelper();
            evaluator.Revaluate(analyzer);
        });
    }

    public void Revaluate(Analyzer analyzer)
    {
        if (CurrentRevaluationProcess == null) return;

        try
        {
            while (true)
            {
                var job = database.Job.FetchFrom(CurrentRevaluationProcess.StartTime);

                if (job == null) break;

                analyzer.AgenciesByID.TryGetValue(job.AgencyID, out var agency);

                if (agency != null && job.Html != null &&
                    (job.Html.StartsWith("<html") || job.Html.StartsWith("<rerender/>")))
                {
                    job.Html = agency.GetMainHtml(job.Html);
                    database.Job.Save(job, JobFilter.Content | JobFilter.Html);
                }

                EvaluateJobEligibility(job, agency?.JobAcceptabilityChecker);

                lock (revaluation_lock)
                    CurrentRevaluationProcess.Passed++;
            }
        }
        finally
        {
            lock (revaluation_lock)
            {
                if (CurrentRevaluationProcess != null)
                {
                    CurrentRevaluationProcess.ProcessCount--;
                    if (CurrentRevaluationProcess.ProcessCount < 1)
                        CurrentRevaluationProcess = null;
                }
            }
        }
    }

    public JobState EvaluateJobEligibility(Job job, Regex? job_acceptability_check)
    {
        // The state of the current job always should be set because it was converted to 'Revaluation'
        var filter = JobFilter.State;

        if (job.Content != null)
        {
            job.Log = string.Empty;
            job.Score = null;

            filter |= JobFilter.Log | JobFilter.Options | JobFilter.Score;
            var user_changes = job.State > JobState.Attention;

            var job_expired = job_acceptability_check?.IsMatch(job.Content);
            var correct_language = job_expired != true ? LanguageIsMatch(job) : (bool?)null;
            var rejected = correct_language != true;
            var eligibility = !rejected && EvaluateEligibility(job, out rejected);

            if (job_expired == true)
            {
                job.Log = @"Expired!" + (string.IsNullOrEmpty(job.Log) ? "" : "\n") + job.Log;
            }

            if (!user_changes)
            {
                if (!eligibility) job.State = JobState.NotApproved;
                else job.State = JobState.Attention;
            }

            if (rejected || job.Score < MinEligibilityScore)
            {
                filter |= JobFilter.Html | JobFilter.Content;
                job.Html = null;
                job.Content = null;
            }

            Log.Information("Job ({0}): state={1} score={2} expired={4} lang={3}",
                job.Code, job.State.ToString(), job.Score,
                correct_language?.ToString() ?? "?",
                job_expired?.ToString() ?? "?");
            Log.Debug("Job ({0}): log={1}", job.Code, job.Log);
        }

        database.Job.Save(job, filter);

        return job.State;
    }

    public void Dispose()
    {
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    public static string GetTextContent(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var root = doc.DocumentNode;
        var buffer = new StringBuilder();
        foreach (var node in root.DescendantsAndSelf())
        {
            if (node.HasChildNodes) continue;
            if (invalid_tag.Contains(node.ParentNode.Name)) continue;

            string text = node.InnerText;
            if (!string.IsNullOrEmpty(text))
                buffer.Append(' ').Append(text.Trim());
        }

        return remove_new_lines.Replace(buffer.ToString(), "\n");
    }

    private bool LanguageIsMatch(Job job)
    {
        if (job.Content == null) return false;

        var word_set = words.Matches(job.Content)
                            .Select(c => c.Value.ToLower())
                            .OrderBy(w => w)
                            .ToHashSet();

        var total_count = word_set.Count;
        if (total_count < 1) return false;

        var splited = word_set.Select((w, i) => new { Word = w, Index = i })
                              .GroupBy(k => k.Index / 100)
                              .Select(g => g.Select(w => w.Word).ToArray())
                              .ToArray();

        var langu_count = 0L;
        foreach (var set in splited)
            langu_count += dictionaries.EnglishCount(set);

        var point = 100 * langu_count / (double)total_count;
        job.Log += string.Format("English: ({0}%)\n\n", (int)point);

        return 50 <= point;
    }

    private bool EvaluateEligibility(Job job, out bool rejected)
    {
        job.Score = 0L;
        var option_scores = new Dictionary<string, List<(JobOption option, int score, string matched)>>();
        var hasField = false;
        rejected = false;

        foreach (var option in options)
        {
            var score = (int)CheckOptionIn(job, option, out var matched);

            if (matched.Length > 0)
            {
                switch (option.Category)
                {
                    case "field":
                        hasField |= true;
                        break;
                    case "reject":
                        rejected |= true;
                        continue;
                }

                if (!option_scores.TryGetValue(option.Category, out var list))
                    option_scores.Add(option.Category, list = []);

                list.Add((option, score, matched));
            }
        }
        
        var resume = new ResumeContext();
        var logs = new List<string>();

        foreach (var category in option_scores)
        {
            logs.Add($"*{category.Key[0..1].ToUpper() + category.Key[1..]}:*");
            logs.Add("");

            category.Value.Sort((a, b) => b.score.CompareTo(a.score));

            var keys = new Dictionary<string, int>();
            var list = new List<(int optionScore, int index, JobOption option, int score, string matched)>();

            var index = 0;
            var factor = 1F;
            foreach (var calc in category.Value)
            {
                calc.option.AddKeyword(resume, calc.matched, out var key);

                int score;
                if (keys.TryGetValue(key, out var calc_score)) score = 0;
                else
                {
                    keys.Add(key, calc.score);
                    calc_score = calc.score;
                    score = (int)(calc.score * factor);

                    job.Score += score;
                    factor = 0.5F;
                }

                list.Add((calc_score, ++index, calc.option, score, calc.matched));
            }

            list.Sort((a, b) =>
            {
                var result = b.optionScore.CompareTo(a.optionScore);
                if (result != 0) return result;
                else return b.index.CompareTo(a.index) * -1;
            });

            foreach (var log in list)
            {
                logs.Add($"**{log.option.ToString('+', log.score)}**");
                logs.Add(log.matched);
                logs.Add("");
            }
        }

        job.Log += string.Join("\n", logs);
        job.Options = resume;
        job.Options.CheckSize();

        if (!hasField || rejected) return false;
        else return job.Score >= MinEligibilityScore;
    }

    private static long CheckOptionIn(Job job, JobOption option, out string matched)
    {
        var score = 0L;
        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match matched_option in option.Pattern.Matches(job.Content ?? "").Cast<Match>())
        {
            if (string.IsNullOrWhiteSpace(matched_option.Value))
                continue;

            if (matched_option.Success)
                matches.Add(matched_option.Value);

            if (score <= 0)
                score = option.Category switch
                {
                    "salary" => EvaluateSalaryScore(matched_option, option),
                    _ => option.Score
                };
        }

        matched = string.Join("\n", matches);
        return score;
    }

    private static long EvaluateSalaryScore(Match matched, JobOption option)
    {
        if (option.Settings is null)
        {
            Log.Warning("Invalid salary options");
            return 0;
        }

        var money_index = (int)option.Settings.money;
        var period_index = (int)option.Settings.period;

        var money_matched = matched.Groups[money_index];

        if (money_matched.Index - matched.Index > 24)
            return 1; // have the min score

        var salary_text = money_matched.Value;
        if (salary_text.ToLower().EndsWith("k"))
            salary_text = salary_text[0..^1] + "000";

        if (!double.TryParse(salary_text.Replace(",", ""), out double salary))
            return 1; // have the min score

        string? period = null;

        if (period_index < 0 &&
            matched.Groups[period_index].Index - (matched.Index + matched.Length) <= 24)
        {
            period = matched.Groups[period_index].Value;
        }

        switch (period)
        {
            case "year":
                salary /= 12;
                break;
            case "month":
                break;
            default:
                // check the salary range
                if (salary > 35000)
                    salary /= 12;
                break;
        }

        return ((long)(salary / 1000) * option.Score);
    }

    public class RevaluationProcess(DateTime start_time, int total_count)
    {
        public DateTime StartTime { get; } = start_time;

        public string StartTimeTitle { get; } = start_time.ToString();

        public long TotalCount { get; } = total_count;

        public int ProcessCount { get; internal set; }

        public int Passed { get; internal set; }

        public object GetReportObject()
        {
            return new
            {
                TrendID = -1,
                Agency = $"Revaluation ({ProcessCount})",
                Link = string.Empty,
                Type = Passed.ToString(),
                State = TotalCount.ToString(),
                LastActivity = StartTimeTitle,
            };
        }
    }
}
