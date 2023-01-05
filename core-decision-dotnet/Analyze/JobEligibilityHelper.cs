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

        public JobEligibilityHelper()
        {
            dictionaries = Dictionaries.Open();
            database = Database.Open();
            options = database.JobOption.FetchAll();
        }

        public void Revaluate()
        {
            var start_time = DateTime.Now.AddSeconds(-1);

            while (true)
                try
                {
                    database.BeginTransaction();

                    var job = database.Job.FetchFrom(start_time);

                    if (job == null) break;

                    EvaluateJobEligibility(job);

                    database.Job.Save(job);

                    database.Commit();
                }
                finally
                {
                    database.Rollback();
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
                // if (node.HasChildNodes) continue;

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

                if (!hasField && option_score > 0 && option.Category == "field")
                {
                    hasField = true;
                }

                job.Score += option_score;
            }

            job.Log += string.Join("\n", logs);

            if (!hasField) return false;
            else return job.Score >= MinEligibilityScore;
        }

        private static long CheckOptionIn(Job job, JobOption option, out string? matched)
        {
            var matched_option = option.Pattern.Match(job.Content ?? "");
            if (!matched_option.Success)
            {
                matched = null;
                return 0;
            }
            else matched = matched_option.Value;

            return option.Category switch
            {
                "salary" => EvaluateSalaryScore(matched_option, option),
                _ => option.Score
            };
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

            var money = matched.Groups[money_index].Value;
            if (!double.TryParse(money?.Replace(",", ""), out double salary)) return 0;
            var period = matched.Groups[period_index].Value;

            switch (period)
            {
                case "year":
                    salary /= 12;
                    break;
                case "month":
                    break;
                default: return 0;
            }

            salary /= 1000;

            return ((long)salary * option.Score);
        }
    }
}
