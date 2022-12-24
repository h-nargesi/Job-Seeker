using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

public class Database : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly SqliteCommand executer;
    private static readonly Regex reg_parameter = new(@"$[\w_]+");

    private JobBusiness? job_business;
    private AgencyBusiness? agency_business;
    private JobOptionBusiness? job_option_business;

    private Database(SqliteConnection connection, SqliteCommand executer)
    {
        this.connection = connection;
        this.executer = executer;
    }

    public long LastInsertRowId()
    {
        executer.CommandText = "SELECT last_insert_rowid()";
        return (long)(executer.ExecuteScalar() ?? throw new Exception("No ID found!"));
    }

    public static Database Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = "hello.db"

        }.ToString());
        var executer = connection.CreateCommand();

        return new Database(connection, executer);
    }

    public Database ClearParameter()
    {
        executer.Parameters.Clear();
        return this;
    }

    public Database Parameter(string name, object value)
    {
        var type = value.GetType();

        if (!name.StartsWith("$")) name = "$" + name;

        SqliteParameter parameter;
        if (executer.Parameters.Contains(name))
        {
            parameter = executer.Parameters[name];
            parameter.SqliteType = DbType.GetSqliteType(type);
        }
        else
        {
            parameter = new SqliteParameter()
            {
                ParameterName = name,
                SqliteType = DbType.GetSqliteType(type)
            };
            executer.Parameters.Add(parameter);
        }

        return this;
    }

    public int Execute(string command, params object?[] parameters)
    {
        executer.CommandText = command;
        AddParameters(command, parameters);
        return executer.ExecuteNonQuery();
    }

    public SqliteDataReader Read(string query, params object?[] parameters)
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

    private void AddParameters(string query, object?[] parameters)
    {
        var index = 0;
        foreach (Match param in reg_parameter.Matches(query))
            Parameter(param.Value, parameters[index++] ?? DBNull.Value);
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
}