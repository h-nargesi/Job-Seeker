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

                logs.Add((option_score > 0 ? "+" : "-") + option.ToString());

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
                var group = int.Parse(option.Options.Split("-")[2]);
                var salary = long.Parse(option.Pattern.Match(job.Html ?? "").Groups[group].Value);
                return salary * option.Score;
            }
            else return option.Score;
        }
    }
}
