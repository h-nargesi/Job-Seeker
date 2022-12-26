using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Photon.JobSeeker
{
    public class Database : IDisposable
    {
        private readonly SqliteConnection connection;
        private readonly SqliteCommand executer;
        private static string? connection_string;
        private static readonly Regex reg_parameter = new(@"\$[\w_]+");

        private JobBusiness? job_business;
        private AgencyBusiness? agency_business;
        private JobOptionBusiness? job_option_business;

        private Database(SqliteConnection connection, SqliteCommand executer)
        {
            this.connection = connection;
            this.executer = executer;
        }

        public static void SetConfiguration(string path, string? version = null, string? password = null, bool? foreign_keys = true)
        {
            connection_string = $"Data Source={path}";
            if (version != null) connection_string += $";Version={version}";
            if (password != null) connection_string += $";Password={password}";
            if (foreign_keys != null) connection_string += $";Foreign Keys={foreign_keys}";
        }

        public static Database Open()
        {
            if (connection_string == null)
                throw new Exception("The configuration is not set.");

            var connection = new SqliteConnection(connection_string);
            var executer = connection.CreateCommand();

            connection.Open();

            return new Database(connection, executer);
        }

        public long LastInsertRowId()
        {
            executer.CommandText = "SELECT last_insert_rowid()";
            return (long)(executer.ExecuteScalar() ?? throw new Exception("No ID found!"));
        }

        public Database ClearParameter()
        {
            executer.Parameters.Clear();
            return this;
        }

        public Database Parameter(string name, object value)
        {
            SqliteType type;

            (type, value) = DbType.GetSqliteType(value);

            if (!name.StartsWith("$")) name = "$" + name;

            SqliteParameter parameter;
            if (executer.Parameters.Contains(name))
            {
                parameter = executer.Parameters[name];
                parameter.SqliteType = type;
                parameter.Value = value;
            }
            else
            {
                executer.Parameters.Add(new SqliteParameter()
                {
                    ParameterName = name,
                    SqliteType = type,
                    Value = value,
                });
            }

            return this;
        }

        public int Execute(string command, params object[] parameters)
        {
            executer.CommandText = command;
            AddParameters(command, parameters);
            return executer.ExecuteNonQuery();
        }

        public SqliteDataReader Read(string query, params object[] parameters)
        {
            executer.CommandText = query;
            AddParameters(query, parameters);
            return executer.ExecuteReader();
        }

        public void Dispose()
        {
            connection.Dispose();
            executer.Dispose();
        }

        internal JobBusiness Job
        {
            get
            {
                if (job_business == null)
                    job_business = new JobBusiness(this);

                return job_business;
            }
        }

        internal AgencyBusiness Agency
        {
            get
            {
                if (agency_business == null)
                    agency_business = new AgencyBusiness(this);

                return agency_business;
            }
        }

        internal JobOptionBusiness JobOption
        {
            get
            {
                if (job_option_business == null)
                    job_option_business = new JobOptionBusiness(this);

                return job_option_business;
            }
        }

        internal void Insert(string name, object job, Enum filter, string? conflict = null)
        {
            var columns = new List<string>();
            var parameters = new List<string>();
            var values = new List<object>();

            foreach (var property in job.GetType().GetProperties())
            {
                if (!CheckPropertyInFilter(filter, name)) continue;

                columns.Add(property.Name);
                parameters.Add("$" + property.Name);

                values.Add(ValueFromProperty(property, job));
            }

            if (values.Count == 0)
                throw new Exception("No column found for insert");

            var query = string.Format(Q_INSERT, name, string.Join(", ", columns), string.Join(", ", parameters), conflict);
            Execute(query, values.ToArray());
        }

        internal void Update(string name, object job, long id, Enum filter)
        {
            var parameters = new List<string>();
            var values = new List<object>();

            foreach (var property in job.GetType().GetProperties())
            {
                if (!CheckPropertyInFilter(filter, name)) continue;

                parameters.Add(property.Name + " = $" + property.Name);

                values.Add(ValueFromProperty(property, job));
            }

            if (values.Count == 0)
                throw new Exception("No column found for update");

            values.Add(id);

            var query = string.Format(Q_UPDATE, name, string.Join(", ", parameters), $"{name}ID = ${name}ID");
            Execute(query, values.ToArray());
        }

        private bool CheckPropertyInFilter(Enum filter, string name)
        {
            if (!Enum.TryParse(filter.GetType(), name, true, out var flag)) return false;
            if (flag == null) return false;
            if (!filter.HasFlag((Enum)flag)) return false;

            return true;
        }

        private object ValueFromProperty(PropertyInfo property, object obj)
        {
            object? value;

            if (property.PropertyType == typeof(JobState))
                value = property.GetValue(obj)?.ToString();

            else value = property.GetValue(obj);

            return value ?? property.MemberType;
        }

        private void AddParameters(string query, object[] parameters)
        {
            var index = 0;
            foreach (Match param in reg_parameter.Matches(query))
                Parameter(param.Value, parameters[index++]);
        }

        private const string Q_INSERT = @"
INSERT INTO {0} ({1}) VALUES ({2}) {3}";

        private const string Q_UPDATE = @"
UPDATE {0} SET {1} WHERE {2}";
    }
}