namespace Photon.JobSeeker
{
    partial class JobBusiness
    {
        public const string GroupAgency = "agency";

        public const string GroupCountry = "country";

        public List<MonitorStageRow> Stages(string group)
        {
            var sql = group == GroupCountry ? Q_STAGES_COUNTRY : Q_STAGES_AGENCY;
            return database.Query<StageRow>(sql)
                .Select(WithRates)
                .ToList();
        }

        public MonitorVerdictDistribution VerdictDistribution()
        {
            return database.QueryFirstOrDefault<MonitorVerdictDistribution>(Q_VERDICTS)
                ?? new MonitorVerdictDistribution();
        }

        public MonitorPendingAges PendingAges()
        {
            return database.QueryFirstOrDefault<MonitorPendingAges>(Q_PENDING_AGES)
                ?? new MonitorPendingAges();
        }

        private static MonitorStageRow WithRates(StageRow row)
        {
            var ai_lane = row.AiPending + row.NotApprovedAI + row.AIError + row.Attention + row.Rejected + row.Applied;
            var judged = row.NotApprovedAI + row.Attention + row.Rejected + row.Applied;
            return new MonitorStageRow(
                row.Label,
                row.Saved,
                row.Revaluation,
                row.NotApprovedRegex,
                row.AiPending,
                row.NotApprovedAI,
                row.AIError,
                row.Attention,
                row.Rejected,
                row.Applied,
                Percent(ai_lane, ai_lane + row.NotApprovedRegex),
                Percent(row.Attention, judged),
                Percent(row.Applied + row.Rejected, row.Attention));
        }

        private static string Percent(long part, long whole)
        {
            return whole == 0 ? "n/a" : $"{part * 100.0 / whole:0.#}%";
        }

        private sealed class StageRow
        {
            public string Label { get; set; } = string.Empty;
            public long Saved { get; set; }
            public long Revaluation { get; set; }
            public long NotApprovedRegex { get; set; }
            public long AiPending { get; set; }
            public long NotApprovedAI { get; set; }
            public long AIError { get; set; }
            public long Attention { get; set; }
            public long Rejected { get; set; }
            public long Applied { get; set; }
        }

        private const string Q_STAGE_COLUMNS = $@"
    COALESCE(SUM(CASE WHEN State = '{nameof(JobState.Saved)}' THEN 1 ELSE 0 END), 0) AS Saved,
    COALESCE(SUM(CASE WHEN State = '{nameof(JobState.Revaluation)}' THEN 1 ELSE 0 END), 0) AS Revaluation,
    COALESCE(SUM(CASE WHEN State = '{nameof(JobState.NotApprovedRegex)}' THEN 1 ELSE 0 END), 0) AS NotApprovedRegex,
    COALESCE(SUM(CASE WHEN State = '{nameof(JobState.AiPending)}' THEN 1 ELSE 0 END), 0) AS AiPending,
    COALESCE(SUM(CASE WHEN State = '{nameof(JobState.NotApprovedAI)}' THEN 1 ELSE 0 END), 0) AS NotApprovedAI,
    COALESCE(SUM(CASE WHEN State = '{nameof(JobState.AIError)}' THEN 1 ELSE 0 END), 0) AS AIError,
    COALESCE(SUM(CASE WHEN State = '{nameof(JobState.Attention)}' THEN 1 ELSE 0 END), 0) AS Attention,
    COALESCE(SUM(CASE WHEN State = '{nameof(JobState.Rejected)}' THEN 1 ELSE 0 END), 0) AS Rejected,
    COALESCE(SUM(CASE WHEN State = '{nameof(JobState.Applied)}' THEN 1 ELSE 0 END), 0) AS Applied";

        private const string Q_STAGES_AGENCY = $@"
SELECT Agency.Title AS Label,{Q_STAGE_COLUMNS}
FROM Job
JOIN Agency ON Agency.AgencyID = Job.AgencyID
GROUP BY Agency.Title
ORDER BY Agency.Title";

        private const string Q_STAGES_COUNTRY = $@"
SELECT COALESCE(NULLIF(Country, ''), '(none)') AS Label,{Q_STAGE_COLUMNS}
FROM Job
GROUP BY COALESCE(NULLIF(Country, ''), '(none)')
ORDER BY Label";

        private const string Q_VERDICTS = $@"
SELECT
    COALESCE(SUM(CASE WHEN AiVerdict = '{nameof(AiVerdict.StrongMatch)}' THEN 1 ELSE 0 END), 0) AS StrongMatch,
    COALESCE(SUM(CASE WHEN AiVerdict = '{nameof(AiVerdict.Match)}' THEN 1 ELSE 0 END), 0) AS ""Match"",
    COALESCE(SUM(CASE WHEN AiVerdict = '{nameof(AiVerdict.Possible)}' THEN 1 ELSE 0 END), 0) AS Possible,
    COALESCE(SUM(CASE WHEN AiVerdict = '{nameof(AiVerdict.NoMatch)}' THEN 1 ELSE 0 END), 0) AS NoMatch,
    COALESCE(SUM(CASE WHEN AiVerdict = '{nameof(AiVerdict.Error)}' THEN 1 ELSE 0 END), 0) AS Error
FROM Job
WHERE AiVerdict IS NOT NULL";

        private const string Q_PENDING_AGES = $@"
SELECT
    COALESCE(SUM(CASE WHEN hours <= 6 THEN 1 ELSE 0 END), 0) AS To6Hours,
    COALESCE(SUM(CASE WHEN hours > 6 AND hours <= 24 THEN 1 ELSE 0 END), 0) AS From6To24Hours,
    COALESCE(SUM(CASE WHEN hours > 24 AND hours <= 72 THEN 1 ELSE 0 END), 0) AS From1To3Days,
    COALESCE(SUM(CASE WHEN hours > 72 THEN 1 ELSE 0 END), 0) AS Over3Days
FROM (
    SELECT (julianday('now', 'localtime') - julianday(RegTime)) * 24.0 AS hours
    FROM Job
    WHERE State = '{nameof(JobState.AiPending)}'
)";
    }
}
