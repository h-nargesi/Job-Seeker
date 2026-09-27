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

        private static object StatsWindow(int days) => new { from = DateTime.Now.Date.AddDays(-(days - 1)) };

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
    }
}
