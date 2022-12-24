using Microsoft.Data.Sqlite;

public class Database : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly SqliteCommand executer;

    public Database()
    {
        connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = "hello.db"

        }.ToString());
        executer = connection.CreateCommand();
    }

    public static Database Open()
    {
        return new Database();
    }

    public Database Parameter(string name, object value)
    {

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
        AddParameters( query, parameters);
        return executer.ExecuteReader();
    }

    public void Dispose()
    {
        connection.Dispose();
        executer.Dispose();
    }

    private void AddParameters(string query, object[] parameters)
    {

    }
}