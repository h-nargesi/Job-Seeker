using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

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

                if (!hasField && option_score > 0 && option.Options == "field")
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
            var matched_option = option.Pattern.Match(job.Html ?? "");
            if (!matched_option.Success)
            {
                matched = null;
                return 0;
            }
            else matched = matched_option.Value;

            if (option.Options.StartsWith("salary"))
            {
                var options = option.Options.Split("-")
                                            .Skip(1)
                                            .Select(o => int.Parse(o))
                                            .ToArray();

                var money = matched_option.Groups[options[0]].Value;
                if (!double.TryParse(money?.Replace(",", ""), out var salary)) return 0;
                var factor = matched_option.Groups[options[1]].Value;

                switch (factor)
                {
                    case "year":
                        salary /= 12;
                        break;
                    case "month":
                        break;
                    default: return 0;
                }

                salary /= 1000;

                return ((int)salary * option.Score);
            }
            else return option.Score;
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
    }
}
