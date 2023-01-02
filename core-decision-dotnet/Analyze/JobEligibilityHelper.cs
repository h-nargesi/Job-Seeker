using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Serilog;

namespace Photon.JobSeeker
{
    public class JobEligibilityHelper : IDisposable
    {
        private readonly static Regex remove_new_lines = new(@"(?<=\n)[\n\s]+");
        public const long MinEligibilityScore = 100;

        private readonly Database database;
        private JobOption[] options;

        public JobEligibilityHelper()
        {
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
            var eligibility = EvaluateEligibility(job);

            if (!eligibility) job.State = JobState.rejected;
            else job.State = JobState.attention;

            Log.Information("Job ({1}): score={0}", job.Score, job.Code);
            Log.Debug("Job ({1}): log={0}", job.Log, job.Code);
            database.Job.Save(job, JobFilter.Log | JobFilter.State | JobFilter.Score);

            return job.State;
        }

        public void Dispose()
        {
            database.Dispose();
        }

        public static string GetHtmlContent(string html)
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
                    buffer.Append(" ").Append(text.Trim());
            }

            return remove_new_lines.Replace(buffer.ToString(), "\n");
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

            job.Log = string.Join("\n", logs);

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
