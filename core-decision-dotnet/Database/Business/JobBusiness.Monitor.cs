namespace Photon.JobSeeker
{
    partial class JobBusiness
    {
        public MonitorCalibration Calibration()
        {
            var floor = database.AppSetting.Floor();
            var row = database.QueryFirstOrDefault<MonitorCalibrationRow>(Q_CALIBRATION)
                ?? new MonitorCalibrationRow();

            var inverted = database.ExecuteScalar<long>(Q_INVERTED_SALARY);
            var keyword_rich = database.ExecuteScalar<long>(Q_KEYWORD_RICH_NO_SKILLS, new { floor });

            var judged = row.Judged ?? 0;
            return new MonitorCalibration(
                judged,
                row.BandMismatches ?? 0,
                inverted,
                keyword_rich,
                floor,
                [
                    Coverage("Seniority", judged, row.SeniorityMissing ?? 0),
                    Coverage("Work model", judged, row.WorkModelMissing ?? 0),
                    Coverage("Contract", judged, row.ContractMissing ?? 0),
                    Coverage("Period", judged, row.PeriodMissing ?? 0),
                    Coverage("Relocation", judged, row.RelocationMissing ?? 0),
                    Coverage("Experience", judged, row.ExperienceMissing ?? 0),
                    Coverage("Salary", judged, row.SalaryMissing ?? 0),
                    Coverage("Skills", judged, row.SkillsMissing ?? 0),
                ]);
        }

        public MonitorQueueHealth QueueHealth()
        {
            return database.QueryFirstOrDefault<MonitorQueueHealth>(Q_QUEUE_HEALTH)
                ?? new MonitorQueueHealth(0, 0, null);
        }

        private static MonitorFieldCoverage Coverage(string field, long judged, long missing)
        {
            return new MonitorFieldCoverage(
                field, judged, missing,
                judged == 0 ? 0 : Math.Round(missing * 100.0 / judged, 1));
        }

        private sealed class MonitorCalibrationRow
        {
            public long? Judged { get; set; }
            public long? BandMismatches { get; set; }
            public long? SeniorityMissing { get; set; }
            public long? WorkModelMissing { get; set; }
            public long? ContractMissing { get; set; }
            public long? PeriodMissing { get; set; }
            public long? RelocationMissing { get; set; }
            public long? ExperienceMissing { get; set; }
            public long? SalaryMissing { get; set; }
            public long? SkillsMissing { get; set; }
        }

        private const string Q_CALIBRATION = $@"
SELECT
    COUNT(*) AS Judged,
    SUM(CASE WHEN AiVerdict <> CASE
            WHEN AiScore >= 85 THEN '{nameof(AiVerdict.StrongMatch)}'
            WHEN AiScore >= 70 THEN '{nameof(AiVerdict.Match)}'
            WHEN AiScore >= 50 THEN '{nameof(AiVerdict.Possible)}'
            ELSE '{nameof(AiVerdict.NoMatch)}'
        END THEN 1 ELSE 0 END) AS BandMismatches,
    SUM(CASE WHEN AiSeniority IS NULL OR AiSeniority = '{nameof(AiSeniority.Unknown)}' THEN 1 ELSE 0 END) AS SeniorityMissing,
    SUM(CASE WHEN AiWorkModel IS NULL OR AiWorkModel = '{nameof(AiWorkModel.Unknown)}' THEN 1 ELSE 0 END) AS WorkModelMissing,
    SUM(CASE WHEN AiContract IS NULL OR AiContract = '{nameof(AiContract.Unknown)}' THEN 1 ELSE 0 END) AS ContractMissing,
    SUM(CASE WHEN AiPeriod IS NULL OR AiPeriod = '{nameof(AiPeriod.Unknown)}' THEN 1 ELSE 0 END) AS PeriodMissing,
    SUM(CASE WHEN AiRelocation IS NULL OR AiRelocation = '{nameof(AiRelocation.Unknown)}' THEN 1 ELSE 0 END) AS RelocationMissing,
    SUM(CASE WHEN AiExperienceYears IS NULL THEN 1 ELSE 0 END) AS ExperienceMissing,
    SUM(CASE WHEN AiSalaryMin IS NULL AND AiSalaryMax IS NULL THEN 1 ELSE 0 END) AS SalaryMissing,
    SUM(CASE WHEN AiSkills IS NULL OR AiSkills = '[]' OR AiSkills = '' THEN 1 ELSE 0 END) AS SkillsMissing
FROM Job
WHERE AiVerdict IS NOT NULL AND AiVerdict <> '{nameof(AiVerdict.Error)}'";

        private const string Q_INVERTED_SALARY = @"
SELECT COUNT(*) FROM Job
WHERE AiSalaryMin IS NOT NULL AND AiSalaryMax IS NOT NULL AND AiSalaryMin > AiSalaryMax";

        private const string Q_KEYWORD_RICH_NO_SKILLS = $@"
SELECT COUNT(*) FROM Job
WHERE Score >= @floor
  AND AiVerdict IS NOT NULL AND AiVerdict <> '{nameof(AiVerdict.Error)}'
  AND (AiSkills IS NULL OR AiSkills = '[]' OR AiSkills = '')";

        private const string Q_QUEUE_HEALTH = $@"
SELECT
    COALESCE(SUM(CASE WHEN State = '{nameof(JobState.AiPending)}' THEN 1 ELSE 0 END), 0) AS AiPending,
    COALESCE(SUM(CASE WHEN State = '{nameof(JobState.AIError)}' THEN 1 ELSE 0 END), 0) AS AiError,
    MIN(CASE WHEN State = '{nameof(JobState.AiPending)}' THEN RegTime END) AS OldestPending
FROM Job";
    }
}
