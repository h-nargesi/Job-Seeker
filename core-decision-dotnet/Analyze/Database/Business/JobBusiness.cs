using Microsoft.Data.Sqlite;

namespace Photon.JobSeeker
{
    class JobBusiness : BaseBusiness<Job>
    {
        public JobBusiness(Database database) : base(database) { }

        public List<Job> Fetch(JobState state)
        {
            using var reader = database.Read(Q_INDEX, state.ToString());
            var list = new List<Job>();

            while (reader.Read())
            {
                var job = ReadJob(reader);
                job.AgencyName = (string)reader[nameof(Job.AgencyName)];
                list.Add(job);
            }

            return list;
        }

        public Job? Fetch(long agency_id, string code)
        {
            using var reader = database.Read(Q_FETCH, agency_id, code);

            if (!reader.Read()) return default;

            return ReadJob(reader);
        }

        public string? GetFirstJob(long agency_id)
        {
            using var reader = database.Read(Q_FETCH_FIRST, agency_id);

            if (!reader.Read()) return default;

            return (string)reader["Url"];
        }

        protected override string[]? GetUniqueColumns { get; } = new string[] {
            nameof(JobFilter.AgencyID), nameof(JobFilter.Code)
        };

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

        private static Job ReadJob(SqliteDataReader reader)
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
                Html = reader[nameof(Job.Html)] as string,
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

        private const string Q_FETCH_FIRST = @"
SELECT Url FROM Job WHERE AgencyID = $agency AND State = 'saved' ORDER BY JobID LIMIT 1";

    }
}