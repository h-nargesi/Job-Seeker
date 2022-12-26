using Microsoft.Data.Sqlite;

namespace Photon.JobSeeker
{
    class JobBusiness
    {
        private Database database;
        public JobBusiness(Database database) => this.database = database;

        public List<Job> Fetch(JobState state)
        {
            using var reader = database.Read(Q_INDEX, state);
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
                Insert(model, filter);

                if (job != null)
                    job.JobID = database.LastInsertRowId();
            }
            else Update(model, id, filter);
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

        private void Insert(object job, JobFilter filter)
        {
            var columns = new List<string>();
            var parameters = new List<string>();
            var values = new List<object?>();

            foreach (var property in job.GetType().GetProperties())
            {
                var flag = Enum.Parse<JobFilter>(property.Name);
                if (!filter.HasFlag(flag)) continue;

                columns.Add(flag.ToString());
                parameters.Add("$" + flag.ToString());

                if (property.PropertyType == typeof(JobState))
                    values.Add(property.GetValue(job)?.ToString());

                else values.Add(property.GetValue(job));
            }

            if (values.Count == 0)
                throw new Exception("No column found for insert");

            var query = string.Format(Q_INSERT, string.Join(", ", columns), string.Join(", ", parameters));
            database.Execute(query, values.ToArray());
        }

        private void Update(object job, long id, JobFilter filter)
        {
            var parameters = new List<string>();
            var values = new List<object?>();

            foreach (var property in job.GetType().GetProperties())
            {
                var flag = Enum.Parse<JobFilter>(property.Name);
                if (!filter.HasFlag(flag)) continue;

                parameters.Add(flag + " = $" + flag);

                if (property.PropertyType == typeof(JobState))
                    values.Add(property.GetValue(job)?.ToString());

                else values.Add(property.GetValue(job));
            }

            if (values.Count == 0)
                throw new Exception("No column found for update");

            values.Add(id);

            var query = string.Format(Q_UPDATE, string.Join(", ", parameters), "JobID = $JobID");
            database.Execute(query, values.ToArray());
        }

        private const string Q_INDEX = @"
SELECT Job.*, Agency.Title as AgencyName
ROM Job JOIN Agency ON Job.AgencyID = Agency.AgencyID
WHERE State = @state
ORDER BY Score DESC, RegTime DESC";

        private const string Q_FETCH = @"
SELECT * FROM Job WHERE AgencyID = $agency and Code = $code";

        private const string Q_INSERT = @"
INSERT INTO Job ({0}) VALUES ({1})
ON CONFLICT(AgencyID, Code) DO NOTHING;";

        private const string Q_UPDATE = @"
UPDATE Job SET {0} WHERE {1}";

    }
}