namespace Photon.JobSeeker
{
    partial class JobBusiness
    {
        private readonly static string Q_INSERT_FROM_SEARCH = $@"
INSERT INTO Job (AgencyID, Country, Url, Code, State)
VALUES (@agencyId, @country, @url, @code, '{nameof(JobState.Saved)}')
ON CONFLICT(AgencyID, Code) DO NOTHING;";

        private readonly static string Q_INSERT_JOB = @"
INSERT INTO Job (AgencyID, Country, Code, Title, State, Score, Url, Html, Content, Link, Log, Options, Tries, PublishedAt)
VALUES (@agencyId, @country, @code, @title, @state, @score, @url, @html, @content, @link, @log, @options, @tries, @publishedAt)
ON CONFLICT(AgencyID, Code) DO NOTHING;";

        private readonly static string Q_UPDATE_CONTENT = @"
UPDATE Job SET Html = @html, Content = @content, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_REGISTER_ATTEMPT = @"
UPDATE Job SET Tries = @tries, Attempts = @attempt, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_CHANGE_STATE = $@"
UPDATE Job SET State = @state, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_MARK_APPLIED = $@"
UPDATE Job SET State = '{nameof(JobState.Applied)}', Log = @log, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_REMOVE_HTML = @"
UPDATE Job SET Html = null, Content = null, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_CHANGE_OPTIONS = @"
UPDATE Job SET Options = @options, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_SAVE_RESUME_TEXT = @"
UPDATE Job SET ResumeText = @resumeText, ModifiedOn = @now
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
               WHEN '{nameof(JobState.Attention)}' THEN 1
               WHEN '{nameof(JobState.AiPending)}' THEN 2
               WHEN '{nameof(JobState.NotApprovedAI)}' THEN 3
               WHEN '{nameof(JobState.Applied)}' THEN 4
               WHEN '{nameof(JobState.Rejected)}' THEN 4
               WHEN '{nameof(JobState.NotApprovedRegex)}' THEN 5
               WHEN '{nameof(JobState.AIError)}' THEN 6
               ELSE 12
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
       WHEN 4 THEN ROW_NUMBER() OVER(PARTITION BY Category ORDER BY ModifiedOn DESC, EffectiveScore DESC, COALESCE(PublishedAt, RegTime) DESC)
       ELSE ROW_NUMBER() OVER(PARTITION BY Category ORDER BY EffectiveScore DESC, COALESCE(PublishedAt, RegTime) DESC)
       END AS Ordering
FROM (
    SELECT *
        , CASE Category
          WHEN 4 THEN ROW_NUMBER() OVER(PARTITION BY AgencyID, State ORDER BY ModifiedOn DESC, EffectiveScore DESC, COALESCE(PublishedAt, RegTime) DESC)
          ELSE ROW_NUMBER() OVER(PARTITION BY AgencyID, State ORDER BY EffectiveScore DESC, COALESCE(PublishedAt, RegTime) DESC)
          END AS Ranking
    FROM ranking
) job
WHERE Ranking <= CASE Category
    WHEN 1 THEN 12
    WHEN 2 THEN 6
    WHEN 3 THEN 6
    WHEN 4 THEN 3
    WHEN 5 THEN 6
    WHEN 6 THEN 3
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

        private const string Q_VACUUM = "vacuum;";
    }
}
