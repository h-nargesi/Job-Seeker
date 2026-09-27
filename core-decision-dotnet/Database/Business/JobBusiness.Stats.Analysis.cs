using Dapper;

namespace Photon.JobSeeker
{
    partial class JobBusiness
    {
        private static readonly string[] ScoreBinLabels =
            Enumerable.Range(0, 10).Select(i => i == 9 ? "90-100" : $"{i * 10}-{i * 10 + 9}").ToArray();

        private static readonly (string Label, long Max)[] ExperienceBuckets =
        [
            ("0-2", 2),
            ("3-5", 5),
            ("6-9", 9),
            ("10-14", 14),
            ("15+", long.MaxValue)
        ];

        private static readonly (string Label, double Max)[] AgingBuckets =
        [
            ("0-2", 2.0),
            ("2-7", 7.0),
            ("7-14", 14.0),
            (">14", double.MaxValue)
        ];

        private static readonly (string Label, double Max)[] DispositionBuckets =
        [
            ("0-1", 1),
            ("2-3", 3),
            ("4-7", 7),
            ("8-14", 14),
            ("15+", double.MaxValue)
        ];

        private static readonly HashSet<JobState> EvaluatedStates =
            [JobState.NotApprovedAI, JobState.Attention, JobState.Applied, JobState.Rejected];

        private static readonly HashSet<JobState> GatePassedStates =
            [JobState.Attention, JobState.Applied, JobState.Rejected];

        public StatsScoreHistograms StatsScoreHistograms(string[] agencyTitles, string[] countryCodes)
        {
            var parameters = new DynamicParameters();
            var and = StatsJobFilter(agencyTitles, countryCodes, parameters);

            var rows = database.Query<ScoreRow>(Q_STATS_SCORES.Replace("@and@", and), parameters);
            var cap = database.AppSetting.ScoreCap();

            var regexBins = new long[10];
            var aiBins = new long[10];
            long regexJobs = 0, aiJobs = 0;

            foreach (var row in rows)
            {
                if (row.Score != null)
                {
                    regexBins[ScoreBin(JobRanking.RegexNorm(row.Score, cap))]++;
                    regexJobs++;
                }

                if (row.AiScore != null)
                {
                    aiBins[ScoreBin(row.AiScore.Value)]++;
                    aiJobs++;
                }
            }

            return new StatsScoreHistograms(
                database.AppSetting.AiPassmark(),
                regexJobs,
                aiJobs,
                [.. ScoreBinLabels],
                [.. regexBins],
                [.. aiBins]);
        }

        public List<StatsDonutChart> StatsAiDonuts(string[] agencyTitles, string[] countryCodes)
        {
            var parameters = new DynamicParameters();
            var and = StatsJobFilter(agencyTitles, countryCodes, parameters);

            var rows = database.Query<AiFieldsRow>(Q_STATS_AI_FIELDS.Replace("@and@", and), parameters);

            return
            [
                Donut("workModel", "Work model", rows, row => row.AiWorkModel),
                Donut("relocation", "Relocation", rows, row => row.AiRelocation),
                Donut("seniority", "Seniority", rows, row => row.AiSeniority),
                Donut("contract", "Contract", rows, row => row.AiContract)
            ];
        }

        public StatsDonutChart StatsAiVerdict(string[] agencyTitles, string[] countryCodes)
        {
            var parameters = new DynamicParameters();
            var and = StatsJobFilter(agencyTitles, countryCodes, parameters);

            var rows = database.Query<VerdictRow>(Q_STATS_VERDICTS.Replace("@and@", and), parameters)
                .ToDictionary(row => row.Verdict, row => row.Jobs);

            var slices = Enum.GetValues<AiVerdict>()
                .Where(verdict => rows.ContainsKey(verdict))
                .Select(verdict => new StatsDonutSlice(verdict.ToString(), rows[verdict]))
                .ToList();

            return new StatsDonutChart("verdict", "AiVerdict", slices);
        }

        public List<StatsBucketItem> StatsCompetitiveness(string[] agencyTitles, string[] countryCodes)
        {
            var parameters = RankingParameters();
            var and = StatsJobFilter(agencyTitles, countryCodes, parameters);

            var rows = database.Query<ExperienceRow>(Q_STATS_EXPERIENCE.Replace("@and@", and), parameters)
                .GroupBy(row => ExperienceBucketIndex(row.AiExperienceYears))
                .ToDictionary(group => group.Key, group => group.ToList());

            return ExperienceBuckets.Select((bucket, index) =>
            {
                var evaluated = (rows.GetValueOrDefault(index) ?? [])
                    .Where(job => EvaluatedStates.Contains(job.State)).ToList();
                var passed = evaluated.Count(job => GatePassedStates.Contains(job.State));

                return new StatsBucketItem(
                    bucket.Label,
                    evaluated.Count,
                    passed,
                    Rate(passed, evaluated.Count),
                    evaluated.Count == 0 ? null : Round(evaluated.Average(job => job.EffectiveScore)));
            }).ToList();
        }

        private static int ExperienceBucketIndex(long years)
        {
            for (var i = 0; i < ExperienceBuckets.Length; i++)
                if (years <= ExperienceBuckets[i].Max) return i;

            return ExperienceBuckets.Length - 1;
        }

        public StatsBucketList StatsAttentionAging(string[] agencyTitles, string[] countryCodes)
        {
            var parameters = new DynamicParameters();
            parameters.Add("now", DateTime.Now);
            var and = StatsJobFilter(agencyTitles, countryCodes, parameters);

            var ages = database.Query<double>(Q_STATS_ATTENTION_AGES.Replace("@and@", and), parameters).ToList();

            return new StatsBucketList(ages.Count, BucketCounts(ages, AgingBuckets, age => age));
        }

        public StatsDisposition StatsDispositionTimes(string[] agencyTitles, string[] countryCodes)
        {
            var parameters = new DynamicParameters();
            var and = StatsJobFilter(agencyTitles, countryCodes, parameters);

            var days = database.Query<double>(Q_STATS_DISPOSITION_DAYS.Replace("@and@", and), parameters)
                .Select(day => Math.Max(0, day))
                .OrderBy(day => day)
                .ToList();

            return new StatsDisposition(
                days.Count,
                Median(days),
                Percentile(days, 0.9),
                BucketCounts(days, DispositionBuckets, day => Math.Floor(day)));
        }

        private static StatsDonutChart Donut<T>(string key, string title, IEnumerable<AiFieldsRow> rows,
            Func<AiFieldsRow, T?> value) where T : struct, Enum
        {
            var counts = new Dictionary<T, long>();

            foreach (var row in rows)
            {
                var item = value(row);
                if (item != null) counts[item.Value] = counts.GetValueOrDefault(item.Value) + 1;
            }

            var slices = Enum.GetValues<T>()
                .Where(entry => counts.ContainsKey(entry))
                .Select(entry => new StatsDonutSlice(entry.ToString(), counts[entry]))
                .ToList();

            return new StatsDonutChart(key, title, slices);
        }

        private static List<StatsBucketCount> BucketCounts<T>(List<T> values, (string Label, double Max)[] buckets,
            Func<T, double> bucketValue)
        {
            var counts = new long[buckets.Length];

            foreach (var value in values)
            {
                var number = bucketValue(value);
                for (var i = 0; i < buckets.Length; i++)
                {
                    if (number <= buckets[i].Max)
                    {
                        counts[i]++;
                        break;
                    }
                }
            }

            return buckets.Select((bucket, i) => new StatsBucketCount(bucket.Label, counts[i])).ToList();
        }

        private static int ScoreBin(double score) => (int)Math.Clamp(Math.Floor(score / 10), 0, 9);

        private static double? Median(List<double> sorted)
        {
            if (sorted.Count == 0) return null;
            if (sorted.Count % 2 == 1) return Round(sorted[sorted.Count / 2]);

            return Round((sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2);
        }

        private static double? Percentile(List<double> sorted, double quantile)
        {
            if (sorted.Count == 0) return null;

            var rank = (int)Math.Ceiling(quantile * sorted.Count);
            var index = Math.Clamp(rank - 1, 0, sorted.Count - 1);

            return Round(sorted[index]);
        }

        private sealed class ScoreRow
        {
            public long? Score { get; set; }

            public int? AiScore { get; set; }
        }

        private sealed class AiFieldsRow
        {
            public AiWorkModel? AiWorkModel { get; set; }

            public AiRelocation? AiRelocation { get; set; }

            public AiSeniority? AiSeniority { get; set; }

            public AiContract? AiContract { get; set; }
        }

        private sealed class VerdictRow
        {
            public AiVerdict Verdict { get; set; }

            public long Jobs { get; set; }
        }

        private sealed class ExperienceRow
        {
            public JobState State { get; set; }

            public long AiExperienceYears { get; set; }

            public double EffectiveScore { get; set; }
        }
    }
}
