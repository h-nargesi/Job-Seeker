using Dapper;

namespace Photon.JobSeeker
{
    partial class JobBusiness
    {
        public List<DailyStackedItem> StatsDailyStacked(int days)
        {
            var rows = database.Query<StatsDailyRow>(Q_STATS_DAILY_STACKED, StatsWindow(days))
                .ToDictionary(row => row.Day);

            return StatsWindowDays(days).Select(day =>
            {
                rows.TryGetValue(day, out var row);
                return new DailyStackedItem(
                    day,
                    row?.Saved ?? 0,
                    row?.Revaluation ?? 0,
                    row?.GateRejected ?? 0,
                    row?.InAi ?? 0,
                    row?.Attention ?? 0,
                    row?.Applied ?? 0,
                    row?.Rejected ?? 0);
            }).ToList();
        }

        public List<VelocityItem> StatsVelocity(int days)
        {
            var rows = database.Query<VelocityRow>(Q_STATS_VELOCITY, StatsWindow(days))
                .ToDictionary(row => row.Day);

            return StatsWindowDays(days).Select(day =>
            {
                rows.TryGetValue(day, out var row);
                return new VelocityItem(day, row?.Applied ?? 0, row?.Rejected ?? 0);
            }).ToList();
        }

        public StatsKpis StatsKpis()
        {
            var row = database.QueryFirstOrDefault<StatsKpiRow>(Q_STATS_KPIS, new { now = DateTime.Now })
                ?? new StatsKpiRow();

            return new StatsKpis(
                Round(row.AvgDispositionDays),
                row.AttentionBacklog ?? 0,
                Round(row.AttentionAvgAgeDays));
        }

        public List<StatsAgencyYieldItem> StatsAgencyYield(string[] agencyTitles)
        {
            var parameters = new DynamicParameters();
            var where = StatsAgencyWhere(agencyTitles, parameters);

            var rows = database.Query<StatsAgencyYieldRow>(Q_STATS_AGENCY_YIELD.Replace("@where@", where), parameters);

            return rows.Select(row => new StatsAgencyYieldItem(
                row.AgencyID,
                row.Title,
                row.JobCount,
                row.Analyzed,
                row.Accepted,
                row.Applied,
                Rate(row.Analyzed, row.JobCount),
                Rate(row.Accepted, row.Analyzed))).ToList();
        }

        public StatsFunnel StatsFunnel(List<StatsAgencyYieldItem> yield)
        {
            return new StatsFunnel(
                new StatsFunnelStage(
                    yield.Sum(row => row.JobCount),
                    yield.Sum(row => row.Analyzed),
                    yield.Sum(row => row.Accepted),
                    yield.Sum(row => row.Applied)),
                yield.Where(row => row.JobCount > 0)
                    .Select(row => new StatsFunnelAgency(row.Title,
                        new StatsFunnelStage(row.JobCount, row.Analyzed, row.Accepted, row.Applied)))
                    .ToList());
        }

        public List<PipelineHealthItem> StatsPipelineHealth(int days, string[] agencyTitles, string[] countryCodes)
        {
            var parameters = StatsWindow(days);
            var and = StatsJobFilter(agencyTitles, countryCodes, parameters);

            var rows = database.Query<PipelineHealthRow>(Q_STATS_PIPELINE_HEALTH.Replace("@and@", and), parameters)
                .ToDictionary(row => row.Day);

            return StatsWindowDays(days).Select(day =>
            {
                rows.TryGetValue(day, out var row);
                return new PipelineHealthItem(day, row?.AiPending ?? 0, row?.AiError ?? 0);
            }).ToList();
        }

        public StatsSkillsGap StatsSkillsGap(string[] agencyTitles, string[] countryCodes)
        {
            var parameters = new DynamicParameters();
            var and = StatsJobFilter(agencyTitles, countryCodes, parameters);

            var rows = database.Query<SkillsRow>(Q_STATS_SKILLS.Replace("@and@", and), parameters).ToList();

            var aliases = SkillNormalizer.BuildAliasMap(database.AppSetting.SkillAliases());
            var known = SkillNormalizer.KnownSkills(ResumeHtml.MasterContext(), aliases);
            var patterns = database.JobOption.FetchAll()
                .Where(option => SkillCategories.Contains(option.Category.ToLowerInvariant()))
                .Select(option => option.Pattern)
                .ToArray();

            var counts = new Dictionary<string, long>();

            foreach (var row in rows)
            {
                foreach (var skill in (row.Skills ?? [])
                    .Where(skill => !string.IsNullOrWhiteSpace(skill))
                    .Select(skill => SkillNormalizer.Normalize(skill, aliases))
                    .Where(skill => skill.Length > 0)
                    .Distinct())
                {
                    counts[skill] = counts.GetValueOrDefault(skill) + 1;
                }
            }

            var top = counts.OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Take(SkillsTop)
                .Select(pair => new SkillsGapItem(
                    pair.Key,
                    pair.Value,
                    known.Contains(pair.Key) || patterns.Any(pattern => pattern.IsMatch(pair.Key))))
                .ToList();

            return new StatsSkillsGap(rows.Count, top);
        }

        private const int SkillsTop = 30;

        private static readonly HashSet<string> SkillCategories = new(StringComparer.Ordinal)
        {
            "field",
            "tech",
            "resume"
        };

        private static long Rate(long value, long total) => total == 0 ? 0 : (long)(100.0 * value / total);

        private static string StatsAgencyWhere(string[] agencyTitles, DynamicParameters parameters)
        {
            if (agencyTitles.Length == 0) return string.Empty;

            var titles = string.Join(", ", agencyTitles.Select((_, i) => $"@a{i}"));
            for (var i = 0; i < agencyTitles.Length; i++)
                parameters.Add($"a{i}", agencyTitles[i]);

            return $" WHERE agc.Title IN ({titles})";
        }

        private static string StatsJobFilter(string[] agencyTitles, string[] countryCodes, DynamicParameters parameters)
        {
            var and = string.Empty;

            if (agencyTitles.Length > 0)
            {
                and += $" AND Agency.Title IN ({string.Join(", ", agencyTitles.Select((_, i) => $"@a{i}"))})";
                for (var i = 0; i < agencyTitles.Length; i++)
                    parameters.Add($"a{i}", agencyTitles[i]);
            }

            if (countryCodes.Length > 0)
            {
                and += $" AND Job.Country IN ({string.Join(", ", countryCodes.Select((_, i) => $"@c{i}"))})";
                for (var i = 0; i < countryCodes.Length; i++)
                    parameters.Add($"c{i}", countryCodes[i]);
            }

            return and;
        }

        private static DynamicParameters StatsWindow(int days)
        {
            var parameters = new DynamicParameters();
            parameters.Add("from", DateTime.Now.Date.AddDays(-(days - 1)));
            return parameters;
        }

        private static IEnumerable<string> StatsWindowDays(int days)
        {
            var start = DateTime.Now.Date.AddDays(-(days - 1));

            for (var offset = 0; offset < days; offset++)
                yield return start.AddDays(offset).ToString("yyyy-MM-dd");
        }

        private static double? Round(double? value) => value == null ? null : Math.Round(value.Value, 1);

        private sealed class StatsDailyRow
        {
            public string Day { get; set; } = string.Empty;

            public long Saved { get; set; }

            public long Revaluation { get; set; }

            public long GateRejected { get; set; }

            public long InAi { get; set; }

            public long Attention { get; set; }

            public long Applied { get; set; }

            public long Rejected { get; set; }
        }

        private sealed class VelocityRow
        {
            public string Day { get; set; } = string.Empty;

            public long Applied { get; set; }

            public long Rejected { get; set; }
        }

        private sealed class StatsKpiRow
        {
            public double? AvgDispositionDays { get; set; }

            public long? AttentionBacklog { get; set; }

            public double? AttentionAvgAgeDays { get; set; }
        }

        private sealed class StatsAgencyYieldRow
        {
            public long AgencyID { get; set; }

            public string Title { get; set; } = string.Empty;

            public long JobCount { get; set; }

            public long Analyzed { get; set; }

            public long Accepted { get; set; }

            public long Applied { get; set; }
        }

        private sealed class PipelineHealthRow
        {
            public string Day { get; set; } = string.Empty;

            public long AiPending { get; set; }

            public long AiError { get; set; }
        }

        private sealed class SkillsRow
        {
            public List<string>? Skills { get; set; }
        }
    }
}
