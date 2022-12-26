using Microsoft.Data.Sqlite;

namespace Photon.JobSeeker
{
    class JobBusiness
    {
        private Database database;
        public JobBusiness(Database database) => this.database = database;

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

        public Job? Fetch(long agency, string code)
        {
            using var reader = database.Read(Q_FETCH, agency, code);

            if (!reader.Read()) return default;

            return ReadJob(reader);
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
                database.Insert("Job", model, filter,
                    "ON CONFLICT(AgencyID, Code) DO NOTHING;");

                if (job != null)
                    job.JobID = database.LastInsertRowId();
            }
            else database.Update("", model, id, filter);
        }

        private static Job ReadJob(SqliteDataReader reader)
        {
            return new Job
            {
                JobID = (long)reader[nameof(Job.JobID)],
                RegTime = DateTime.Parse((string)reader[nameof(Job.RegTime)]),
                AgencyID = (long)reader[nameof(Job.AgencyID)],
                Code = (string)reader[nameof(Job.Code)],
                Title = (string)reader[nameof(Job.Title)],
                State = Enum.Parse<JobState>((string)reader[nameof(Job.State)]),
                Score = reader[nameof(Job.Score)] as long?,
                Url = (string)reader[nameof(Job.Url)],
                Html = (string)reader[nameof(Job.Html)],
                Link = (string)reader[nameof(Job.Link)],
                Log = (string)reader[nameof(Job.Log)],
            };
        }

        private const string Q_INDEX = @"
SELECT Job.*, Agency.Title as AgencyName
FROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
WHERE State = $state
ORDER BY Score DESC, RegTime DESC";

        private const string Q_FETCH = @"
SELECT * FROM Job WHERE AgencyID = $agency and Code = $code";

    }
}