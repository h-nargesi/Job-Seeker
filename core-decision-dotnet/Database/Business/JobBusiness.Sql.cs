namespace Photon.JobSeeker
{
    partial class JobBusiness
    {
        private const string ModifiedOnGuard =
            $"ModifiedOn = CASE WHEN State IN ('{nameof(JobState.Applied)}', '{nameof(JobState.Rejected)}')"
            + " THEN ModifiedOn ELSE @now END";

        private readonly static string Q_INSERT_FROM_SEARCH = $@"
INSERT INTO Job (AgencyID, Country, Url, Code, State, RegTime, ModifiedOn)
VALUES (@agencyId, @country, @url, @code, '{nameof(JobState.Saved)}', @now, @now)
ON CONFLICT(AgencyID, Code) DO NOTHING;";

        private readonly static string Q_INSERT_JOB = @"
INSERT INTO Job (AgencyID, Country, Code, Title, State, Score, Url, Html, Content, Link, Log, Options, Tries, PublishedAt, RegTime, ModifiedOn)
VALUES (@agencyId, @country, @code, @title, @state, @score, @url, @html, @content, @link, @log, @options, @tries, @publishedAt, @now, @now)
ON CONFLICT(AgencyID, Code) DO NOTHING;";

        private readonly static string Q_UPDATE_CONTENT = $@"
UPDATE Job SET Html = @html, Content = @content, {ModifiedOnGuard}
WHERE JobID = @jobId";

        private readonly static string Q_REGISTER_ATTEMPT = $@"
UPDATE Job SET Tries = @tries, Attempts = @attempt, {ModifiedOnGuard}
WHERE JobID = @jobId";

        private readonly static string Q_CHANGE_STATE = $@"
UPDATE Job SET State = @state, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_MANUAL_STATE = @"
UPDATE Job SET State = @state, Log = @log, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_MARK_APPLIED = $@"
UPDATE Job SET State = '{nameof(JobState.Applied)}', Log = @log, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_REMOVE_HTML = $@"
UPDATE Job SET Html = null, Content = null, {ModifiedOnGuard}
WHERE JobID = @jobId";

        private readonly static string Q_CHANGE_OPTIONS = $@"
UPDATE Job SET Options = @options, {ModifiedOnGuard}
WHERE JobID = @jobId";

        private readonly static string Q_SAVE_RESUME_TEXT = $@"
UPDATE Job SET ResumeText = @resumeText, {ModifiedOnGuard}
WHERE JobID = @jobId";

        private const string Q_DELETE = @"
DELETE FROM Job WHERE JobID = @jobId";

        private readonly static string Q_INDEX = @$"
WITH date_diff AS (
    SELECT job.*
         , MAX(0, JulianDay(latest.LatestTime) - COALESCE(JulianDay(job.PublishedAt), JulianDay(job.RegTime))) AS AgeDays
    FROM (
        SELECT Job.JobID, Job.RegTime, Job.ModifiedOn, Job.AgencyID, Job.Code, Job.Title
             , Job.State, Job.Score, Job.AiScore, job.Country, Job.Url, Job.Link
             , Job.AiRelocation, Job.AiWorkModel, Job.PublishedAt
             , Agency.Title AS AgencyName
             , CASE State
               WHEN '{nameof(JobState.Attention)}' THEN 'Attention'
               WHEN '{nameof(JobState.AiPending)}' THEN 'AiPending'
               WHEN '{nameof(JobState.AIError)}' THEN 'AIError'
               WHEN '{nameof(JobState.NotApprovedAI)}' THEN 'NotApproved'
               WHEN '{nameof(JobState.NotApprovedRegex)}' THEN 'NotApproved'
               WHEN '{nameof(JobState.Applied)}' THEN 'Done'
               WHEN '{nameof(JobState.Rejected)}' THEN 'Done'
               ELSE 'Other'
               END AS Category
             , SUBSTR(Job.RegTime, 1, 10) AS RegDate
             , CASE WHEN Job.Log IS NULL OR Job.Log = ''
                    THEN -1 WHEN Job.Log LIKE '%) Relocation**%' THEN 1 ELSE 0 END AS Relocation
             , CASE WHEN Job.Log IS NULL OR Job.Log = ''
                    THEN -1 WHEN Job.Log LIKE '%) Remote**%' THEN 1 ELSE 0 END AS Remote
        FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
        @where@
    ) job
    CROSS JOIN (
        SELECT MAX(RegTime) AS LatestTime FROM Job
    ) latest

), ranking AS (
    SELECT job.JobID, job.RegTime, job.ModifiedOn, job.AgencyID, job.Code, job.Title
         , job.State, job.Score, job.AiScore, job.Country, job.Url, job.Link
         , job.AiRelocation, job.AiWorkModel, job.PublishedAt, job.Relocation, job.Remote
         , job.AgencyName, job.Category, job.RegDate
         , {JobRanking.SqlEffectiveScore} AS EffectiveScore
    FROM date_diff job
)

SELECT *
     , CASE Category
       WHEN 'Done' THEN ROW_NUMBER() OVER(PARTITION BY Category ORDER BY ModifiedOn DESC, EffectiveScore DESC, COALESCE(PublishedAt, RegTime) DESC)
       WHEN 'Other' THEN ROW_NUMBER() OVER(PARTITION BY Category ORDER BY ModifiedOn DESC, EffectiveScore DESC, COALESCE(PublishedAt, RegTime) DESC)
       ELSE ROW_NUMBER() OVER(PARTITION BY Category ORDER BY EffectiveScore DESC, COALESCE(PublishedAt, RegTime) DESC)
       END AS Ordering
FROM (
    SELECT *
        , CASE Category
          WHEN 'Other' THEN ROW_NUMBER() OVER(PARTITION BY AgencyID, State ORDER BY EffectiveScore DESC, COALESCE(PublishedAt, RegTime) DESC)
          WHEN 'Done' THEN ROW_NUMBER() OVER(PARTITION BY State ORDER BY ModifiedOn DESC, EffectiveScore DESC, COALESCE(PublishedAt, RegTime) DESC)
          ELSE ROW_NUMBER() OVER(PARTITION BY State ORDER BY EffectiveScore DESC, COALESCE(PublishedAt, RegTime) DESC)
          END AS Ranking
    FROM ranking
) job
WHERE Ranking <= CASE Category
    WHEN 'Attention' THEN 12
    WHEN 'AiPending' THEN 6
    WHEN 'AIError' THEN 3
    WHEN 'NotApproved' THEN 6
    WHEN 'Done' THEN 3
    ELSE 1 END
ORDER BY Category, Ordering";

        private const string Q_FETCH_ID = @"
SELECT * FROM Job WHERE JobID = @job";

        private const string Q_FETCH_BY_CODE = @"
SELECT * FROM Job WHERE AgencyID = @agency and Code = @code";

        private const string Q_FETCH_META = @"
SELECT JobID, State, Log FROM Job WHERE JobID = @job";

        private const string Q_GET_ID_BY_CODE = @"
SELECT JobID FROM Job WHERE AgencyID = @agency and Code = @code";

        private readonly static string RevaluationScope = $@"
State IN (
    '{nameof(JobState.Attention)}',
    '{nameof(JobState.AiPending)}',
    '{nameof(JobState.NotApprovedAI)}',
    '{nameof(JobState.AIError)}'
) AND Content IS NOT NULL AND ModifiedOn <= @date";

        private readonly static string Q_FETCH_FROM = $@"
SELECT * FROM Job WHERE {RevaluationScope} ORDER BY JobID LIMIT 1";

        private readonly static string Q_FETCH_FROM_COUNT = $@"
SELECT COUNT(*) FROM Job WHERE {RevaluationScope}";

        private readonly static string Q_FETCH_UPDATE_REVAL = @$"
UPDATE Job SET State = '{nameof(JobState.Saved)}', Attempts = 0, Tries = NULL, ModifiedOn = @now
WHERE State = '{nameof(JobState.Revaluation)}'";

        private readonly static string Q_RESURRECT = @$"
UPDATE Job SET State = '{nameof(JobState.Saved)}', Attempts = 0, Tries = NULL, ModifiedOn = @now
WHERE State = '{nameof(JobState.NotApprovedRegex)}' AND Score >= @floor";

        private readonly static string Q_FETCH_FIRST = $@"
SELECT JobID, Url, Tries, Attempts, RegTime FROM Job
WHERE AgencyID = @agency AND State = '{nameof(JobState.Saved)}' AND Attempts < 4
ORDER BY Attempts = 0 DESC, Attempts DESC, JobID LIMIT 1";

        private readonly static string Q_CLEAN = @$"
DELETE FROM Job WHERE RegTime < @date AND (State != '{nameof(JobState.Applied)}' OR Attempts >= 4)";

        private readonly static string Q_CLEAN_ATTENTION = @$"
UPDATE Job SET Html = null
WHERE RegTime < @date AND State IN ('{nameof(JobState.Attention)}') AND JobID NOT IN (
    SELECT JobID FROM Job WHERE State IN ('{nameof(JobState.Attention)}')
    ORDER BY {JobRanking.SqlRankScore} DESC LIMIT 0, 100)";

        private readonly static string Q_CLEAN_NOT_APPROVED = @$"
UPDATE Job SET Html = null, Content = null WHERE RegTime < @date AND State IN (
    '{nameof(JobState.NotApprovedRegex)}',
    '{nameof(JobState.NotApprovedAI)}',
    '{nameof(JobState.AIError)}')";

        private readonly static string Q_STATS_DAILY_STACKED = $@"
SELECT SUBSTR(RegTime, 1, 10) AS Day
     , SUM(CASE WHEN State = '{nameof(JobState.Saved)}' THEN 1 ELSE 0 END) AS Saved
     , SUM(CASE WHEN State = '{nameof(JobState.Revaluation)}' THEN 1 ELSE 0 END) AS Revaluation
     , SUM(CASE WHEN State IN ('{nameof(JobState.NotApprovedRegex)}', '{nameof(JobState.NotApprovedAI)}') THEN 1 ELSE 0 END) AS GateRejected
     , SUM(CASE WHEN State IN ('{nameof(JobState.AiPending)}', '{nameof(JobState.AIError)}') THEN 1 ELSE 0 END) AS InAi
     , SUM(CASE WHEN State = '{nameof(JobState.Attention)}' THEN 1 ELSE 0 END) AS Attention
     , SUM(CASE WHEN State = '{nameof(JobState.Applied)}' THEN 1 ELSE 0 END) AS Applied
     , SUM(CASE WHEN State = '{nameof(JobState.Rejected)}' THEN 1 ELSE 0 END) AS Rejected
FROM Job
WHERE RegTime >= @from
GROUP BY SUBSTR(RegTime, 1, 10)
ORDER BY Day";

        private readonly static string Q_STATS_VELOCITY = $@"
SELECT SUBSTR(ModifiedOn, 1, 10) AS Day
     , SUM(CASE WHEN State = '{nameof(JobState.Applied)}' THEN 1 ELSE 0 END) AS Applied
     , SUM(CASE WHEN State = '{nameof(JobState.Rejected)}' THEN 1 ELSE 0 END) AS Rejected
FROM Job
WHERE State IN ('{nameof(JobState.Applied)}', '{nameof(JobState.Rejected)}') AND ModifiedOn >= @from
GROUP BY SUBSTR(ModifiedOn, 1, 10)
ORDER BY Day";

        private readonly static string Q_STATS_KPIS = $@"
SELECT AVG(CASE WHEN State IN ('{nameof(JobState.Applied)}', '{nameof(JobState.Rejected)}')
        THEN JulianDay(ModifiedOn) - JulianDay(COALESCE(PublishedAt, RegTime)) END) AS AvgDispositionDays
     , COALESCE(SUM(CASE WHEN State = '{nameof(JobState.Attention)}' THEN 1 ELSE 0 END), 0) AS AttentionBacklog
     , AVG(CASE WHEN State = '{nameof(JobState.Attention)}'
        THEN JulianDay(@now) - JulianDay(COALESCE(PublishedAt, RegTime)) END) AS AttentionAvgAgeDays
FROM Job";

        private readonly static string Q_STATS_AGENCY_YIELD = $@"
SELECT agc.AgencyID, agc.Title
     , IFNULL(job.JobCount, 0) AS JobCount
     , IFNULL(job.Analyzed, 0) AS Analyzed
     , IFNULL(job.Attention, 0) + IFNULL(job.Applied, 0) AS Accepted
     , IFNULL(job.Applied, 0) AS Applied
FROM Agency agc
LEFT JOIN (
    SELECT AgencyID
        , COUNT(*) AS JobCount
        , SUM(CASE State WHEN '{nameof(JobState.Saved)}' THEN 0 ELSE 1 END) AS Analyzed
        , SUM(CASE State WHEN '{nameof(JobState.Attention)}' THEN 1 ELSE 0 END) AS Attention
        , SUM(CASE State WHEN '{nameof(JobState.Applied)}' THEN 1 ELSE 0 END) AS Applied
    FROM Job
    GROUP BY AgencyID
) job ON agc.AgencyID = job.AgencyID
@where@
ORDER BY agc.AgencyID";

        private readonly static string Q_STATS_PIPELINE_HEALTH = $@"
SELECT SUBSTR(ModifiedOn, 1, 10) AS Day
     , SUM(CASE WHEN State = '{nameof(JobState.AiPending)}' THEN 1 ELSE 0 END) AS AiPending
     , SUM(CASE WHEN State = '{nameof(JobState.AIError)}' THEN 1 ELSE 0 END) AS AiError
FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
WHERE State IN ('{nameof(JobState.AiPending)}', '{nameof(JobState.AIError)}') AND ModifiedOn >= @from @and@
GROUP BY SUBSTR(ModifiedOn, 1, 10)
ORDER BY Day";

        private readonly static string Q_STATS_SKILLS = $@"
        SELECT AiSkills AS Skills FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
        WHERE AiSkills IS NOT NULL AND AiSkills != '[]' AND AiSkills != '' @and@";

        private readonly static string Q_STATS_SCORES = @"
        SELECT Score, AiScore FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
        WHERE Score IS NOT NULL OR AiScore IS NOT NULL @and@";

        private readonly static string Q_STATS_AI_FIELDS = $@"
        SELECT AiWorkModel, AiRelocation, AiSeniority, AiContract
        FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
        WHERE State IN ('{nameof(JobState.Attention)}', '{nameof(JobState.Applied)}', '{nameof(JobState.Rejected)}') @and@";

        private readonly static string Q_STATS_VERDICTS = @"
        SELECT AiVerdict AS Verdict, COUNT(*) AS Jobs
        FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
        WHERE AiVerdict IS NOT NULL @and@
        GROUP BY AiVerdict";

        private readonly static string Q_STATS_EXPERIENCE = @$"
        WITH date_diff AS (
            SELECT Job.State, Job.Score, Job.AiScore, Job.AiExperienceYears
                 , MAX(0, JulianDay(latest.LatestTime) - COALESCE(JulianDay(Job.PublishedAt), JulianDay(Job.RegTime))) AS AgeDays
            FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
            CROSS JOIN (
                SELECT MAX(RegTime) AS LatestTime FROM Job
            ) latest
            WHERE Job.AiExperienceYears >= 0 @and@
        )
        SELECT State, AiExperienceYears, {JobRanking.SqlEffectiveScore} AS EffectiveScore
        FROM date_diff";

        private readonly static string Q_STATS_ATTENTION_AGES = $@"
        SELECT JulianDay(@now) - JulianDay(COALESCE(PublishedAt, RegTime)) AS AgeDays
        FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
        WHERE State = '{nameof(JobState.Attention)}' @and@";

        private readonly static string Q_STATS_DISPOSITION_DAYS = $@"
        SELECT JulianDay(ModifiedOn) - JulianDay(COALESCE(PublishedAt, RegTime)) AS Days
        FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
        WHERE State IN ('{nameof(JobState.Applied)}', '{nameof(JobState.Rejected)}') @and@";

        private const string Q_VACUUM = "vacuum;";
    }
}
