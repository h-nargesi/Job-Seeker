using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Serilog;

namespace Photon.JobSeeker
{
    public class JobEligibilityHelper : IDisposable
    {
        private readonly static Regex remove_new_lines = new(@"(?<=\n)[\n\s]+");
        private readonly static Regex words = new(@"[a-zA-Z]{3,}");
        public const long MinEligibilityScore = 100;

        private readonly Dictionaries dictionaries;
        private readonly Database database;
        private readonly JobOption[] options;

        private static object revaluation_lock = new();
        public static RevaluationProcess? CurrentRevaluationProcess { get; private set; }

        public JobEligibilityHelper()
        {
            dictionaries = Dictionaries.Open();
            database = Database.Open();
            options = database.JobOption.FetchAll();
        }

        public void Revaluate()
        {
            lock (revaluation_lock)
            {
                if (CurrentRevaluationProcess == null)
                {
                    var start_time = DateTime.Now.AddSeconds(-1);
                    var total_count = (int)database.Job.FetchFromCount(start_time);
                    CurrentRevaluationProcess = new RevaluationProcess(start_time, total_count);
                }

                CurrentRevaluationProcess.ProcessCount++;
            }

            try
            {
                while (true)
                {
                    var job = database.Job.FetchFrom(CurrentRevaluationProcess.StartTime);

                    if (job == null) break;

                    if (job.Html != null)
                    {
                        var html = job.Html;
                        if (html.StartsWith("<html"))
                            html = job.AgencyID switch
                            {
                                1 => Indeed.IndeedPageJob.GetHtmlContent(job.Html ?? ""),
                                2 => IamExpat.IamExpatPageJob.GetHtmlContent(job.Html ?? ""),
                                3 => LinkedIn.LinkedInPageJob.GetHtmlContent(job.Html ?? ""),
                                _ => "",
                            };

                        job.Content = GetTextContent(html);
                        database.Job.Save(job, JobFilter.Content);
                    }

                    EvaluateJobEligibility(job);

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

        public JobState EvaluateJobEligibility(Job job)
        {
            job.Log = "";

            var correct_language = LanguageIsMatch(job);

            var eligibility = correct_language && EvaluateEligibility(job);

            if (!eligibility) job.State = JobState.Rejected;
            else job.State = JobState.Attention;

            Log.Information("Job ({0}): state={1} score={2} lang={3}", job.State, job.Code, job.Score, correct_language);
            Log.Debug("Job ({0}): log={1}", job.Code, job.Log);
            database.Job.Save(job, JobFilter.Log | JobFilter.State | JobFilter.Score);

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

            var point = (int)(100 * langu_count / (double)total_count);
            job.Log += string.Format("English: ({0}%)\n\n", point);

            return 50 <= point;
        }

        private bool EvaluateEligibility(Job job)
        {
            var logs = new List<string>(options.Length);
            var hasField = false;
            var rejected = false;
            job.Score = 0L;

            foreach (var option in options)
            {
                var option_score = CheckOptionIn(job, option, out var matched);

                if (option_score > 0)
                {
                    logs.Add($"**{option.ToString('+', option_score)}**");
                    logs.Add(matched ?? "?");
                    logs.Add("");
                }

                switch (option.Category)
                {
                    case "field":
                        hasField |= option_score > 0;
                        break;
                    case "rejected":
                        rejected |= option_score > 0;
                        break;
                }

                job.Score += option_score;
            }

            job.Log += string.Join("\n", logs);

            if (!hasField || rejected) return false;
            else return job.Score >= MinEligibilityScore;
        }

        private static long CheckOptionIn(Job job, JobOption option, out string? matched)
        {
            var score = 0L;
            var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match matched_option in option.Pattern.Matches(job.Content ?? ""))
            {
                if (matched_option.Success)
                    matches.Add(matched_option.Value);

                if (score <= 1)
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

            if (!double.TryParse(money_matched.Value?.Replace(",", ""), out double salary))
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

        public class RevaluationProcess
        {
            public RevaluationProcess(DateTime start_time, int total_count)
            {
                StartTime = start_time;
                StartTimeTitle = start_time.ToString();
                TotalCount = total_count;
            }

            public DateTime StartTime { get; }

            public string StartTimeTitle { get; }

            public long TotalCount { get; }

            public int ProcessCount { get; internal set; }

            public int Passed { get; internal set; }

            public object GetReportObject()
            {
                return new
                {
                    TrendID = -1,
                    Agency = $"Revaluation ({ProcessCount})",
                    Link = "",
                    Type = Passed.ToString(),
                    State = TotalCount.ToString(),
                    LastActivity = StartTimeTitle,
                };
            }
        }
    }
}
