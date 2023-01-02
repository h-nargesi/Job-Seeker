using Microsoft.Data.Sqlite;

namespace Photon.JobSeeker
{
    class JobBusiness : BaseBusiness<Job>
    {
        public JobBusiness(Database database) : base(database) { }

        public List<object> Fetch(JobState state)
        {
            using var reader = database.Read(Q_INDEX, state.ToString());
            var list = new List<object>();

            while (reader.Read())
                list.Add(new
                {
                    Job = ReadJob(reader),
                    AgencyName = (string)reader["AgencyName"],
                });

            return list;
        }

        public Job? FetchFrom(DateTime time)
        {
            using var reader = database.Read(Q_FETCH_FROM, time);

            if (!reader.Read()) return default;

            return ReadJob(reader, true);
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

        private const string Q_INDEX = @"
SELECT Job.*, Agency.Title as AgencyName
FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
WHERE State = $state
ORDER BY Score DESC, RegTime DESC";

        private const string Q_FETCH = @"
SELECT * FROM Job WHERE AgencyID = $agency and Code = $code";

        private const string Q_FETCH_FROM = @"
SELECT * FROM Job WHERE ModifiedOn <= $date";

        private const string Q_FETCH_FIRST = @"
SELECT JobID, Url, Tries FROM Job
WHERE AgencyID = $agency AND State = 'saved' AND (Tries IS NULL OR Tries NOT LIKE '%4: %')
ORDER BY Tries IS NULL DESC, Tries DESC, JobID LIMIT 1";

    }
}