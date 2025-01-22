using System.Data.SQLite;
using Newtonsoft.Json;

namespace Photon.JobSeeker
{
    class JobBusiness : BaseBusiness<Job>
    {
        public JobBusiness(Database database) : base(database) { }

        public List<object> Fetch(int[] agencyids, string[] countrycodes)
        {
            var where = string.Empty;

            if (agencyids?.Length > 0)
                where += $" AND Agency.AgencyID IN ({string.Join(",", agencyids)})";

            if (countrycodes?.Length > 0)
                where = $" AND Job.Country IN ('{string.Join("','", countrycodes)}')";

            if (!string.IsNullOrEmpty(where))
                where = "WHERE" + where.Substring(0, 4);

            using var reader = database.Read(Q_INDEX.Replace("@where@", where));
            var list = new List<object>();

            while (reader.Read())
                list.Add(new
                {
                    Job = ReadJob(reader),
                    Relocation = (long)reader["Relocation"] != 0,
                    AgencyName = (string)reader["AgencyName"],
                });

            return list;
        }

        public long FetchFromCount(DateTime time)
        {
            database.Execute(Q_FETCH_UPDATE_REVAL);

            using var reader = database.Read(Q_FETCH_FROM_COUNT, time);
            if (!reader.Read()) return default;
            return (long)reader[0];
        }

        public Job? FetchFrom(DateTime time)
        {
            Job result;
            database.BeginTransaction();
            try
            {
                using (var reader = database.Read(Q_FETCH_FROM, time))
                {
                    if (!reader.Read()) return default;
                    result = ReadJob(reader, true);
                }

                Save(new { result.JobID, State = JobState.Revaluation });
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
            using var reader = database.Read(Q_FETCH_BY_CODE, agency_id, code);

            if (!reader.Read()) return default;

            return ReadJob(reader, true);
        }

        public Job? Fetch(long job_id)
        {
            using var reader = database.Read(Q_FETCH_ID, job_id);

            if (!reader.Read()) return default;

            return ReadJob(reader, true);
        }

        public ResumeContext? FetchOptions(long job_id)
        {
            using var reader = database.Read(Q_FETCH_OPTIONS, job_id);

            if (!reader.Read() || reader[nameof(Job.Options)] is not string options)
            {
                return default;
            }

            return JsonConvert.DeserializeObject<ResumeContext>(options);
        }

        public string? GetFirstJob(long agency_id)
        {
            try
            {
                database.BeginTransaction();

                using var reader = database.Read(Q_FETCH_FIRST, agency_id);

                if (!reader.Read()) return default;

                var data = new
                {
                    JobID = (long)reader[nameof(Job.JobID)],
                    Url = (string)reader[nameof(Job.Url)],
                    Tries = reader[nameof(Job.Tries)] as string,
                };

                reader.Close();

                var prv_tries = data.Tries?.Split("\n");
                var this_time = 1 + (prv_tries?.Length ?? 0);

                var this_tries = string.Join(": ", this_time, DateTime.Now) + (this_time > 1 ? "\n" + data.Tries : "");

                Save(new { data.JobID, Tries = this_tries }, JobFilter.Tries);
                database.Commit();

                return data.Url;
            }
            catch
            {
                database.Rollback();
                throw;
            }
        }

        public void Save(object model, JobFilter filter = JobFilter.All)
        {
            long id;

            var job = model as Job;
            if (job != null) id = job.JobID;
            else
            {
                var id_property = model.GetType().GetProperty(nameof(Job.JobID));
                if (id_property != null)
                    id = (long?)id_property.GetValue(model) ?? default;
                else id = default;
            }

            if (id == default)
            {
                database.Insert(nameof(Job), model, filter,
                    "ON CONFLICT(AgencyID, Code) DO NOTHING;");

                if (job != null)
                    job.JobID = database.LastInsertRowId();
            }
            else database.Update(nameof(Job), model, id, filter);
        }

        public void ChangeState(long id, JobState state)
        {
            Save(new { JobID = id, State = state });
        }

        public void RemoveHtmlContent(long id)
        {
            Save(new { JobID = id, Html = (string?)null, Content = (string?)null });
        }

        public void ChangeOptions(long id, ResumeContext? options)
        {
            Save(new { JobID = id, Options = options });
        }

        public void Clean(int mounths)
        {
            database.Execute(Q_CLEAN, DateTime.Now.AddMonths(-mounths));
            database.Execute(Q_CLEAN_ATTENTION, DateTime.Now.AddDays(-mounths * 7));
            database.Execute(Q_CLEAN_NOT_APPROVED, DateTime.Now.AddDays(-7));
            database.Execute(Q_VACUUM);
        }

        protected override string[]? GetUniqueColumns { get; } = new string[] {
            nameof(JobFilter.AgencyID), nameof(JobFilter.Code)
        };

        private static Job ReadJob(SQLiteDataReader reader, bool full = false)
        {
            var job = new Job
            {
                JobID = (long)reader[nameof(Job.JobID)],
                RegTime = (DateTime)reader[nameof(Job.RegTime)],
                AgencyID = (long)reader[nameof(Job.AgencyID)],
                Code = (string)reader[nameof(Job.Code)],
                Title = reader[nameof(Job.Title)] as string,
                State = Enum.Parse<JobState>((string)reader[nameof(Job.State)]),
                Score = reader[nameof(Job.Score)] as long?,
                Url = (string)reader[nameof(Job.Url)],
                Html = full ? reader[nameof(Job.Html)] as string : null,
                Content = full ? reader[nameof(Job.Content)] as string : null,
                Link = reader[nameof(Job.Link)] as string,
                Log = full ? reader[nameof(Job.Log)] as string : null,
            };

            if (full && reader[nameof(Job.Options)] is string json)
            {
                try { job.Options = JsonConvert.DeserializeObject<ResumeContext>(json); }
                catch { }
            }

            return job;
        }

        /*
         * Sorting Algorithm:

            c=\frac{b\cdot6}{7}
            X=x-b
            Y=ae^{-\frac{X^{2}}{2c^{2}}}
            U=-e^{\left(\frac{2\cdot X}{c}\right)}
            Y+U
        */
        private const int MaxScore = 30;
        private const int DaysPriod = 10;

        private readonly static string Q_INDEX = @$"
WITH date_diff AS (
    SELECT job.*
         , JulianDay(latest.LatestTime) - JulianDay(job.RegTime) - {DaysPriod} AS X
         , MIN({MaxScore}, latest.TopScore) AS A
         , {DaysPriod} * 6 / 7 AS C
    FROM (
        SELECT Job.JobID, Job.RegTime, Job.ModifiedOn, Job.AgencyID, Job.Code, Job.Title
             , Job.State, Job.Score, Job.Url, Job.Link
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
        SELECT MAX(RegTime) AS LatestTime, MAX(Score) / 13 AS TopScore FROM Job
    ) latest

), ranking AS (
    SELECT job.JobID, job.RegTime, job.ModifiedOn, job.AgencyID, job.Code, job.Title
         , job.State, job.Score, job.Url, job.Link, job.Relocation
         , job.AgencyName, job.Category, job.RegDate
         , Score + A * EXP(YF) - EXP(UF) AS RankScore
    FROM (
        SELECT *
             , POWER(X, 2) / (-2 * POWER(C, 2)) AS YF
             , 2 * X / C AS UF
        FROM date_diff
    ) job
)

SELECT *
     , CASE Category
       WHEN 4 THEN ROW_NUMBER() OVER(PARTITION BY Category ORDER BY ModifiedOn DESC, RankScore DESC, RegTime DESC)
       ELSE ROW_NUMBER() OVER(PARTITION BY Category ORDER BY RankScore DESC, RegTime DESC)
       END AS Ordering
FROM (
    SELECT *
        , CASE Category
          WHEN 4 THEN ROW_NUMBER() OVER(PARTITION BY AgencyID, State ORDER BY ModifiedOn DESC, RankScore DESC, RegTime DESC)
          ELSE ROW_NUMBER() OVER(PARTITION BY AgencyID, State ORDER BY RankScore DESC, RegTime DESC)
          END AS Ranking
    FROM ranking
) job
WHERE Ranking <= (12 / Category)
ORDER BY Category, Ordering";

        private const string Q_FETCH_ID = @"
SELECT * FROM Job WHERE JobID = $job";

        private const string Q_FETCH_BY_CODE = @"
SELECT * FROM Job WHERE AgencyID = $agency and Code = $code";

        private const string Q_FETCH_FROM = @$"
SELECT * FROM Job WHERE State != '{nameof(JobState.Revaluation)}' AND Content IS NOT NULL AND ModifiedOn <= $date";

        private const string Q_FETCH_FROM_COUNT = @$"
SELECT COUNT(*) FROM Job WHERE Content IS NOT NULL AND ModifiedOn <= $date";

        private const string Q_FETCH_UPDATE_REVAL = @$"
UPDATE Job SET State = '{nameof(JobState.Saved)}' WHERE State = '{nameof(JobState.Revaluation)}'";

        private const string Q_FETCH_OPTIONS = @"
SELECT Options FROM Job WHERE JobID = $job";

        private const string Q_FETCH_FIRST = @$"
SELECT JobID, Url, Tries FROM Job
WHERE AgencyID = $agency AND State = '{nameof(JobState.Saved)}' AND (Tries IS NULL OR Tries NOT LIKE '%4: %')
ORDER BY Tries IS NULL DESC, Tries DESC, JobID LIMIT 1";

        private const string Q_CLEAN = @$"
DELETE FROM Job WHERE RegTime < $date AND (State != '{nameof(JobState.Applied)}' OR Tries LIKE '%4: %')";

        private const string Q_CLEAN_ATTENTION = @$"
UPDATE Job SET Html = null
WHERE RegTime < $date AND State IN ('{nameof(JobState.Attention)}') AND JobID NOT IN (
    SELECT JobID FROM Job WHERE State IN ('{nameof(JobState.Attention)}')
    ORDER BY Score DESC LIMIT 0, 100)";

        private const string Q_CLEAN_NOT_APPROVED = @$"
UPDATE Job SET Html = null, Content = null WHERE RegTime < $date AND State IN ('{nameof(JobState.NotApproved)}')";

        private const string Q_VACUUM = "vacuum;";

    }
}