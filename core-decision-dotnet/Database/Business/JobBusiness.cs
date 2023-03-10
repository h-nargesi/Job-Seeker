using Microsoft.Data.Sqlite;

namespace Photon.JobSeeker
{
    class JobBusiness : BaseBusiness<Job>
    {
        public JobBusiness(Database database) : base(database) { }

        public List<object> Fetch(int[] agencyids)
        {
            string agencies;
            if (agencyids.Length < 1) agencies = string.Empty;
            else agencies = $"WHERE Agency.AgencyID IN ({string.Join(",", agencyids)})";

            using var reader = database.Read(Q_INDEX.Replace("@where@", agencies));
            var list = new List<object>();

            while (reader.Read())
                list.Add(new
                {
                    Job = ReadJob(reader),
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

                return result;
            }
            finally
            {
                database.Commit();
            }
        }

        public Job? Fetch(long agency_id, string code)
        {
            using var reader = database.Read(Q_FETCH, agency_id, code);

            if (!reader.Read()) return default;

            return ReadJob(reader, true);
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
            finally { database.Rollback(); }
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

        protected override string[]? GetUniqueColumns { get; } = new string[] {
            nameof(JobFilter.AgencyID), nameof(JobFilter.Code)
        };

        private static Job ReadJob(SqliteDataReader reader, bool full = false)
        {
            return new Job
            {
                JobID = (long)reader[nameof(Job.JobID)],
                RegTime = DateTime.Parse((string)reader[nameof(Job.RegTime)]),
                AgencyID = (long)reader[nameof(Job.AgencyID)],
                Code = (string)reader[nameof(Job.Code)],
                Title = reader[nameof(Job.Title)] as string,
                State = Enum.Parse<JobState>((string)reader[nameof(Job.State)]),
                Score = reader[nameof(Job.Score)] as long?,
                Url = (string)reader[nameof(Job.Url)],
                Html = full ? reader[nameof(Job.Html)] as string : null,
                Content = full ? reader[nameof(Job.Content)] as string : null,
                Link = reader[nameof(Job.Link)] as string,
                Log = reader[nameof(Job.Log)] as string,
            };
        }

        private const string Q_INDEX = @$"
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
    FROM (
        SELECT *
             , Score - Cast((JulianDay(job.RegTime) - JulianDay(beginning.TheTime)) As Integer) AS RankScore
        FROM (
            SELECT Job.JobID, Job.RegTime, Job.ModifiedOn, Job.AgencyID, Job.Code, Job.Title
                 , Job.State, Job.Score, Job.Url, Job.Link, Job.Log
                 , Agency.Title as AgencyName
                 , CASE State 
                   WHEN '{nameof(JobState.Attention)}' THEN 1
                   WHEN '{nameof(JobState.NotApproved)}' THEN 2
                   WHEN '{nameof(JobState.Applied)}' THEN 4
                   WHEN '{nameof(JobState.Rejected)}' THEN 4
                   ELSE 12
                   END AS Category
                 , SUBSTR(Job.RegTime, 1, 10) AS RegDate
            FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
        ) job
        CROSS JOIN (
            SELECT MIN(RegTime) AS TheTime FROM Job
        ) beginning
    ) job
) job
WHERE Ranking <= (12 / Category)
ORDER BY Category, Ordering";

        private const string Q_FETCH = @"
SELECT * FROM Job WHERE AgencyID = $agency and Code = $code";

        private const string Q_FETCH_FROM = @$"
SELECT * FROM Job WHERE State != '{nameof(JobState.Revaluation)}' AND Content IS NOT NULL AND ModifiedOn <= $date";

        private const string Q_FETCH_FROM_COUNT = @$"
SELECT COUNT(*) FROM Job WHERE Content IS NOT NULL AND ModifiedOn <= $date";

        private const string Q_FETCH_UPDATE_REVAL = @$"
UPDATE Job SET State = '{nameof(JobState.Saved)}' WHERE State = '{nameof(JobState.Revaluation)}'";

        private const string Q_FETCH_FIRST = @$"
SELECT JobID, Url, Tries FROM Job
WHERE AgencyID = $agency AND State = '{nameof(JobState.Saved)}' AND (Tries IS NULL OR Tries NOT LIKE '%4: %')
ORDER BY Tries IS NULL DESC, Tries DESC, JobID LIMIT 1";

    }
}