using Dapper;
using Newtonsoft.Json;

namespace Photon.JobSeeker
{
    class JobBusiness
    {
        private readonly Database database;

        public JobBusiness(Database database) => this.database = database;

        public List<object> Fetch(string[] agency_titles, string[] country_codes)
        {
            var where = string.Empty;
            var parameters = new DynamicParameters();

            if (agency_titles?.Length > 0)
            {
                where += $" AND Agency.Title IN ({string.Join(", ", agency_titles.Select((_, i) => $"@a{i}"))})";
                for (var i = 0; i < agency_titles.Length; i++)
                    parameters.Add($"a{i}", agency_titles[i]);
            }

            if (country_codes?.Length > 0)
            {
                where += $" AND Job.Country IN ({string.Join(", ", country_codes.Select((_, i) => $"@c{i}"))})";
                for (var i = 0; i < country_codes.Length; i++)
                    parameters.Add($"c{i}", country_codes[i]);
            }

            if (!string.IsNullOrEmpty(where))
                where = "WHERE" + where.Substring(4);

            var rows = database.Query<Job, long, string, (Job Job, bool Relocation, string AgencyName)>(
                Q_INDEX.Replace("@where@", where),
                (job, relocation, agency) => (job, relocation != 0, agency),
                parameters, splitOn: "Relocation,AgencyName");

            return rows.Select(r => (object)new { r.Job, r.Relocation, r.AgencyName }).ToList();
        }

        public long FetchFromCount(DateTime time)
        {
            return database.ExecuteScalar<long>(Q_FETCH_FROM_COUNT, new { date = time });
        }

        public void ResetRevaluations()
        {
            database.Execute(Q_FETCH_UPDATE_REVAL);
        }

        public Job? FetchFrom(DateTime time)
        {
            database.BeginTransaction();
            try
            {
                var result = database.Query<Job>(Q_FETCH_FROM, new { date = time }).FirstOrDefault();
                if (result == null) return default;

                ChangeState(result.JobID, JobState.Revaluation);
                database.Commit();

                return result;
            }
            catch
            {
                database.Rollback();
                throw;
            }
        }

        public Job? Fetch(long agency_id, string code)
        {
            return database.Query<Job>(Q_FETCH_BY_CODE, new { agency = agency_id, code }).FirstOrDefault();
        }

        public Job? Fetch(long job_id)
        {
            return database.Query<Job>(Q_FETCH_ID, new { job = job_id }).FirstOrDefault();
        }

        public ResumeContext? FetchOptions(long job_id)
        {
            var options = database.ExecuteScalar<string?>(Q_FETCH_OPTIONS, new { job = job_id });

            return options == null ? default : JsonConvert.DeserializeObject<ResumeContext>(options);
        }

        public string? GetFirstJob(long agency_id)
        {
            try
            {
                database.BeginTransaction();

                var data = database.Query<FirstJobRow>(Q_FETCH_FIRST, new { agency = agency_id }).FirstOrDefault();
                if (data == null) return default;

                var now = DateTime.Now;
                var prv_tries = data.Tries?.Split("\n");
                var this_time = 1 + (prv_tries?.Length ?? 0);

                var this_tries = $"{this_time}: {now} (age {(int)(now - data.RegTime).TotalDays}d)"
                    + (this_time > 1 ? "\n" + data.Tries : "");

                UpdateTries(data.JobID, this_tries);
                database.Commit();

                return data.Url;
            }
            catch
            {
                database.Rollback();
                throw;
            }
        }

        public void InsertFromSearch(long agencyId, string country, string url, string code)
        {
            database.Execute(Q_INSERT_FROM_SEARCH, new { agencyId, country, url, code });
        }

        public void InsertJob(Job job)
        {
            database.Execute(Q_INSERT_JOB, new
            {
                agencyId = job.AgencyID,
                country = job.Country,
                code = job.Code,
                title = job.Title,
                state = job.State.ToString(),
                score = job.Score,
                url = job.Url,
                html = job.Html,
                content = job.Content,
                link = job.Link,
                log = job.Log,
                options = job.Options,
                tries = job.Tries,
            });

            if (database.Changes() == 1)
                job.JobID = database.LastInsertRowId();
            else
                job.JobID = Fetch(job.AgencyID, job.Code!)?.JobID ?? 0;
        }

        public void UpdateScrapedJob(Job job, bool codeChanged, bool linkFound, bool includeState)
        {
            var sets = new List<string>
            {
                "Title = @title",
                "Country = @country",
                "Html = @html",
                "Content = @content",
            };

            if (codeChanged) sets.Add("Code = @code");
            if (linkFound) sets.Add("Link = @link");
            if (includeState) sets.Add("State = @state");

            database.Execute($@"
UPDATE Job SET {string.Join(", ", sets)}, ModifiedOn = @now
WHERE JobID = @jobId", new
            {
                title = job.Title,
                country = job.Country,
                html = job.Html,
                content = job.Content,
                code = job.Code,
                link = job.Link,
                state = job.State.ToString(),
                now = DateTime.Now,
                jobId = job.JobID,
            });
        }

        public void UpdateStepstoneJob(Job job)
        {
            database.Execute(Q_UPDATE_STEPSTONE, new
            {
                title = job.Title,
                html = job.Html,
                content = job.Content,
                now = DateTime.Now,
                jobId = job.JobID,
            });
        }

        public void UpdateJobContent(Job job)
        {
            database.Execute(Q_UPDATE_CONTENT, new
            {
                html = job.Html,
                content = job.Content,
                now = DateTime.Now,
                jobId = job.JobID,
            });
        }

        public void UpdateEvaluation(Job job, bool clearContent)
        {
            var clear = clearContent ? ", Html = null, Content = null" : "";

            database.Execute($@"
UPDATE Job SET State = @state, Log = @log, Options = @options, Score = @score{clear}, ModifiedOn = @now
WHERE JobID = @jobId", new
            {
                state = job.State.ToString(),
                log = job.Log,
                options = job.Options,
                score = job.Score,
                now = DateTime.Now,
                jobId = job.JobID,
            });
        }

        public void UpdateTries(long id, string tries)
        {
            database.Execute(Q_UPDATE_TRIES, new { tries, now = DateTime.Now, jobId = id });
        }

        public void ChangeState(long id, JobState state)
        {
            database.Execute(Q_CHANGE_STATE, new { state = state.ToString(), now = DateTime.Now, jobId = id });
        }

        public void RemoveHtmlContent(long id)
        {
            database.Execute(Q_REMOVE_HTML, new { now = DateTime.Now, jobId = id });
        }

        public void ChangeOptions(long id, ResumeContext? options)
        {
            database.Execute(Q_CHANGE_OPTIONS, new { options, now = DateTime.Now, jobId = id });
        }

        public void Delete(long id)
        {
            database.Execute(Q_DELETE, new { jobId = id });
        }

        public void Clean(int mounths, bool vacuum = false)
        {
            database.Execute(Q_CLEAN, new { date = DateTime.Now.AddMonths(-mounths) });
            database.Execute(Q_CLEAN_ATTENTION, new { date = DateTime.Now.AddDays(-mounths * 7) });
            database.Execute(Q_CLEAN_NOT_APPROVED, new { date = DateTime.Now.AddDays(-7) });
            if (vacuum) database.Execute(Q_VACUUM);
        }

        private sealed class FirstJobRow
        {
            public long JobID { get; set; }

            public string Url { get; set; } = string.Empty;

            public string? Tries { get; set; }

            public DateTime RegTime { get; set; }
        }

        private readonly static string Q_INSERT_FROM_SEARCH = $@"
INSERT INTO Job (AgencyID, Country, Url, Code, State)
VALUES (@agencyId, @country, @url, @code, '{nameof(JobState.Saved)}')
ON CONFLICT(AgencyID, Code) DO NOTHING;";

        private readonly static string Q_INSERT_JOB = @"
INSERT INTO Job (AgencyID, Country, Code, Title, State, Score, Url, Html, Content, Link, Log, Options, Tries)
VALUES (@agencyId, @country, @code, @title, @state, @score, @url, @html, @content, @link, @log, @options, @tries)
ON CONFLICT(AgencyID, Code) DO NOTHING;";

        private readonly static string Q_UPDATE_STEPSTONE = @"
UPDATE Job SET Title = @title, Html = @html, Content = @content, Tries = NULL, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_UPDATE_CONTENT = @"
UPDATE Job SET Html = @html, Content = @content, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_UPDATE_TRIES = @"
UPDATE Job SET Tries = @tries, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_CHANGE_STATE = $@"
UPDATE Job SET State = @state, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_REMOVE_HTML = @"
UPDATE Job SET Html = null, Content = null, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_CHANGE_OPTIONS = @"
UPDATE Job SET Options = @options, ModifiedOn = @now
WHERE JobID = @jobId";

        private const string Q_DELETE = @"
DELETE FROM Job WHERE JobID = @jobId";

        private readonly static string Q_INDEX = @$"
WITH date_diff AS (
    SELECT job.*
         , MAX(0, JulianDay(latest.LatestTime) - JulianDay(job.RegTime)) AS AgeDays
    FROM (
        SELECT Job.JobID, Job.RegTime, Job.ModifiedOn, Job.AgencyID, Job.Code, Job.Title
             , Job.State, Job.Score, job.Country, Job.Url, Job.Link
             , Agency.Title AS AgencyName
             , CASE State
               WHEN '{nameof(JobState.Attention)}' THEN 1
               WHEN '{nameof(JobState.NotApproved)}' THEN 2
               WHEN '{nameof(JobState.Applied)}' THEN 4
               WHEN '{nameof(JobState.Rejected)}' THEN 4
               ELSE 12
               END AS Category
             , SUBSTR(Job.RegTime, 1, 10) AS RegDate
             , CASE WHEN Job.Log LIKE '%) Relocation**%' THEN 1 ELSE 0 END AS Relocation
        FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
        @where@
    ) job
    CROSS JOIN (
        SELECT MAX(RegTime) AS LatestTime FROM Job
    ) latest

), ranking AS (
    SELECT job.JobID, job.RegTime, job.ModifiedOn, job.AgencyID, job.Code, job.Title
         , job.State, job.Score, job.Country, job.Url, job.Link, job.Relocation
         , job.AgencyName, job.Category, job.RegDate
         -- Mirror of JobRanking.Weight (Analyze/JobRanking.cs). Keep in sync.
         , Score * CASE
               WHEN AgeDays <= 2  THEN 0.85
               WHEN AgeDays <= 4  THEN 0.85 + 0.15 * (AgeDays - 2) / 2
               WHEN AgeDays <= 10 THEN 1.0
               WHEN AgeDays <= 14 THEN 1.0 - 0.25 * (AgeDays - 10) / 4
               WHEN AgeDays <= 28 THEN 0.75 - 0.50 * (AgeDays - 14) / 14
               ELSE 0.15
             END AS EffectiveScore
    FROM date_diff job
)

SELECT *
     , CASE Category
       WHEN 4 THEN ROW_NUMBER() OVER(PARTITION BY Category ORDER BY ModifiedOn DESC, EffectiveScore DESC, RegTime DESC)
       ELSE ROW_NUMBER() OVER(PARTITION BY Category ORDER BY EffectiveScore DESC, RegTime DESC)
       END AS Ordering
FROM (
    SELECT *
        , CASE Category
          WHEN 4 THEN ROW_NUMBER() OVER(PARTITION BY AgencyID, State ORDER BY ModifiedOn DESC, EffectiveScore DESC, RegTime DESC)
          ELSE ROW_NUMBER() OVER(PARTITION BY AgencyID, State ORDER BY EffectiveScore DESC, RegTime DESC)
          END AS Ranking
    FROM ranking
) job
WHERE Ranking <= CASE Category WHEN 1 THEN 12 WHEN 2 THEN 6 WHEN 4 THEN 3 ELSE 1 END
ORDER BY Category, Ordering";

        private const string Q_FETCH_ID = @"
SELECT * FROM Job WHERE JobID = @job";

        private const string Q_FETCH_BY_CODE = @"
SELECT * FROM Job WHERE AgencyID = @agency and Code = @code";

        private readonly static string Q_FETCH_FROM = @$"
SELECT * FROM Job WHERE State != '{nameof(JobState.Revaluation)}' AND Content IS NOT NULL AND ModifiedOn <= @date";

        private readonly static string Q_FETCH_FROM_COUNT = @"
SELECT COUNT(*) FROM Job WHERE Content IS NOT NULL AND ModifiedOn <= @date";

        private readonly static string Q_FETCH_UPDATE_REVAL = @$"
UPDATE Job SET State = '{nameof(JobState.Saved)}' WHERE State = '{nameof(JobState.Revaluation)}'";

        private const string Q_FETCH_OPTIONS = @"
SELECT Options FROM Job WHERE JobID = @job";

        private readonly static string Q_FETCH_FIRST = @$"
SELECT JobID, Url, Tries, RegTime FROM Job
WHERE AgencyID = @agency AND State = '{nameof(JobState.Saved)}' AND (Tries IS NULL OR Tries NOT LIKE '%4: %')
ORDER BY Tries IS NULL DESC, Tries DESC, JobID LIMIT 1";

        private readonly static string Q_CLEAN = @$"
DELETE FROM Job WHERE RegTime < @date AND (State != '{nameof(JobState.Applied)}' OR Tries LIKE '%4: %')";

        // Current behavior: keeps Html for the global top-100 Attention jobs by Score
        // (the subquery is not scoped by the same RegTime window as the outer query).
        private readonly static string Q_CLEAN_ATTENTION = @$"
UPDATE Job SET Html = null
WHERE RegTime < @date AND State IN ('{nameof(JobState.Attention)}') AND JobID NOT IN (
    SELECT JobID FROM Job WHERE State IN ('{nameof(JobState.Attention)}')
    ORDER BY Score DESC LIMIT 0, 100)";

        private readonly static string Q_CLEAN_NOT_APPROVED = @$"
UPDATE Job SET Html = null, Content = null WHERE RegTime < @date AND State IN ('{nameof(JobState.NotApproved)}')";

        private const string Q_VACUUM = "vacuum;";
    }
}
