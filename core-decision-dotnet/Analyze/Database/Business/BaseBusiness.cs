namespace Photon.JobSeeker
{
    abstract class BaseBusiness<T> where T : class
    {
        protected readonly Database database;
        protected BaseBusiness(Database database) => this.database = database;

        protected abstract string[]? GetUniqueColumns { get; }

        public void Save(object model, Enum filter)
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
                var unique = GetUniqueColumns == null ? null :
                    $"ON CONFLICT({string.Join(", ", GetUniqueColumns)}) DO NOTHING;";
                
                database.Insert(nameof(Job), model, filter, unique);

                if (job != null)
                    job.JobID = database.LastInsertRowId();
            }
            else database.Update(nameof(Job), model, id, filter);
        }

        public void Delete(long job_id)
        {
            var name = typeof(T).Name;
            database.Execute($"DELETE FROM {name} WHERE {typeof(T).Name}ID = ${name.ToLower()}", job_id);
        }
    }
}