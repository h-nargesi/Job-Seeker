using System.Reflection;
using System.Text.RegularExpressions;
using System.Data.SQLite;

namespace Photon.JobSeeker
{
    public class Database : IDisposable
    {
        private readonly SQLiteConnection connection;
        private readonly SQLiteCommand executer;
        private SQLiteTransaction? transaction;
        private static string? connection_string;
        private static readonly Regex reg_parameter = new(@"\$[\w_]+");

        private TrendBusiness? trend_business;
        private JobBusiness? job_business;
        private AgencyBusiness? agency_business;
        private JobOptionBusiness? job_option_business;

        public Database(SQLiteConnection connection, SQLiteCommand executer)
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

            var connection = new SQLiteConnection(connection_string);
            var executer = connection.CreateCommand();

            connection.Open();
            
            return new Database(connection, executer);
        }

        public void BeginTransaction()
        {
            transaction = connection.BeginTransaction();
            executer.Transaction = transaction;
        }

        public void Commit()
        {
            transaction?.Commit();
            transaction = null;
            executer.Transaction = null;
        }

        public void Rollback()
        {
            transaction?.Rollback();
            transaction = null;
            executer.Transaction = null;
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
            System.Data.DbType type;

            (type, value) = DbType.GetSqliteType(value);

            if (!name.StartsWith("$")) name = "$" + name;

            SQLiteParameter parameter;
            if (executer.Parameters.Contains(name))
            {
                parameter = executer.Parameters[name];
                parameter.DbType = type;
                parameter.Value = value;
            }
            else
            {
                executer.Parameters.Add(new SQLiteParameter()
                {
                    ParameterName = name,
                    DbType = type,
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

        public SQLiteDataReader Read(string query, params object[] parameters)
        {
            executer.CommandText = query;
            AddParameters(query, parameters);
            return executer.ExecuteReader();
        }

        public List<Dictionary<string, object>> ReadAll(string query)
        {
            using var reader = Read(query);
            var list = new List<Dictionary<string, object>>();
            var columns = GetColumns(reader);

            while (reader.Read())
            {
                var record = new Dictionary<string, object>();
                foreach (var column in columns)
                    record[column] = reader[column];
                list.Add(record);
            }

            return list;
        }

        public void Dispose()
        {
            transaction?.Dispose();
            connection.Dispose();
            executer.Dispose();
            GC.SuppressFinalize(this);
        }

        internal TrendBusiness Trend => trend_business ??= new TrendBusiness(this);

        internal JobBusiness Job => job_business ??= new JobBusiness(this);

        internal AgencyBusiness Agency => agency_business ??= new AgencyBusiness(this);

        internal JobOptionBusiness JobOption => job_option_business ??= new JobOptionBusiness(this);

        internal void Insert(string name, object job, Enum filter, string? conflict = null)
        {
            var columns = new List<string>();
            var parameters = new List<string>();
            var values = new List<object>();

            foreach (var property in job.GetType().GetProperties())
            {
                if (!IsPropertyAllowed(filter, property.Name)) continue;

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
                if (!IsPropertyAllowed(filter, property.Name)) continue;

                parameters.Add($"{property.Name} = ${property.Name}");

                values.Add(ValueFromProperty(property, job));
            }

            if (values.Count == 0)
                throw new Exception("No column found for update");

            if (Enum.TryParse(filter.GetType(), "ModifiedOn", true, out var _))
            {
                parameters.Add($"ModifiedOn = $ModifiedOn");
                values.Add(DateTime.Now);
            }

            values.Add(id);

            var query = string.Format(Q_UPDATE, name, string.Join(", ", parameters), $"{name}ID = ${name}ID");
            Execute(query, values.ToArray());
        }

        internal void Update(string name, object job, long id)
        {
            var parameters = new List<string>();
            var values = new List<object>();

            foreach (var property in job.GetType().GetProperties())
            {
                parameters.Add($"{property.Name} = ${property.Name}");

                values.Add(ValueFromProperty(property, job));
            }

            if (values.Count == 0)
                throw new Exception("No column found for update");

            values.Add(id);

            var query = string.Format(Q_UPDATE, name, string.Join(", ", parameters), $"{name}ID = ${name}ID");
            Execute(query, values.ToArray());
        }

        private static bool IsPropertyAllowed(Enum filter, string name)
        {
            if (!Enum.TryParse(filter.GetType(), name, true, out var flag)) return false;
            if (flag == null) return false;
            return filter.HasFlag((Enum)flag);
        }

        private static object ValueFromProperty(PropertyInfo property, object obj)
        {
            return property.GetValue(obj) ?? property.PropertyType;
        }

        private void AddParameters(string query, object[] parameters)
        {
            var index = 0;
            foreach (var param in reg_parameter.Matches(query).Cast<Match>())
                Parameter(param.Value, parameters[index++]);
        }

        private static string[] GetColumns(SQLiteDataReader reader)
        {
            var result = new string[reader.FieldCount];
            for (var i = 0; i < result.Length; i++)
                result[i] = reader.GetName(i);
            return result;
        }

        private const string Q_INSERT = @"
INSERT INTO {0} ({1}) VALUES ({2}) {3}";

        private const string Q_UPDATE = @"
UPDATE {0} SET {1} WHERE {2}";
    }
}