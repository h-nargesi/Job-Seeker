using System.Text.RegularExpressions;

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
                var option_score = CheckOptionIn(job, option);

                if (option_score > 0)
                    logs.Add(option.ToString('+', option_score));

                if (!hasField && option_score > 0 && option.Options == "field")
                {
                    hasField = true;
                }

                job.Score += option_score;
            }

            job.Log = string.Join("|", logs);

            if (!hasField) return false;
            else return job.Score >= MinEligibilityScore;
        }

        private static long CheckOptionIn(Job job, JobOption option)
        {
            if (!option.Pattern.IsMatch(job.Html ?? "")) return 0;

            if (option.Options.StartsWith("salary"))
            {
                var options = option.Options.Split("-")
                                            .Skip(1)
                                            .Select(o => int.Parse(o))
                                            .ToArray();

                var salary_match = option.Pattern.Match(job.Html ?? "");

                var money = salary_match.Groups[options[0]].Value;
                var salary = double.Parse(money.Replace(",", ""));
                var factor = salary_match.Groups[options[1]].Value;

                switch (factor)
                {
                    case "year":
                        salary /= 12;
                        break;
                }

                salary /= 1000;

                return ((int)salary * option.Score);
            }
            else return option.Score;
        }
    }
}
