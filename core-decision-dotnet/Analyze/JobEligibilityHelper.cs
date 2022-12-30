using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Serilog;

namespace Photon.JobSeeker.Analyze
{
    public static class JobEligibilityHelper
    {
        public const long MinEligibilityScore = 100;

        public static bool EvaluateJobEligibility(Database database, Job job)
        {
            var options = database.JobOption.FetchAll();

            return EvaluateEligibility(job, options);
        }

        public static string GetHtmlContent(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var root = doc.DocumentNode;
            var result = new StringBuilder();
            foreach (var node in root.DescendantsAndSelf())
            {
                // if (node.HasChildNodes) continue;

                string text = node.InnerText;
                if (!string.IsNullOrEmpty(text))
                    result.Append(" ").Append(text.Trim());
            }

            return result.ToString();
        }

        private static bool EvaluateEligibility(Job job, JobOption[] options)
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
            var content = GetHtmlContent(job.Html ?? "");
            var matched_option = option.Pattern.Match(content);
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
