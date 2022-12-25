using Microsoft.Data.Sqlite;

class JobBusiness
{
    private Database database;
    public JobBusiness(Database database) => this.database = database;

    public List<Job> Fetch(JobState state)
    {
        using var reader = database.Read(Q_INDEX, state);
        var list = new List<Job>();

        while (reader.Read())
            list.Add(ReadJob(reader));

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
            JobID = (long)reader["JobID"],
            RegTime = DateTime.Parse((string)reader["RegTime"]),
            AgencyID = (long)reader["AgencyID"],
            Code = (string)reader["Code"],
            Title = (string)reader["Title"],
            State = Enum.Parse<JobState>((string)reader["State"]),
            Score = reader["Score"] as long?,
            Url = (string)reader["Url"],
            Html = (string)reader["Html"],
            Link = (string)reader["Link"],
            Log = (string)reader["Log"],
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
SELECT * FROM Job WHERE State = @state
ORDER BY Score DESC, RegTime DESC";

    private const string Q_FETCH = @"
SELECT * FROM Job WHERE AgencyID = $agency and Code = $code";

    private const string Q_INSERT = @"
INSERT INTO Job ({0}) VALUES ({1})
ON CONFLICT(AgencyID, Code) DO NOTHING;";

    private const string Q_UPDATE = @"
UPDATE Job SET {0} WHERE {1}";

}