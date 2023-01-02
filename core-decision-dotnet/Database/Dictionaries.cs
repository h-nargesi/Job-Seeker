using Microsoft.Data.Sqlite;

namespace Photon.JobSeeker
{
    public class Dictionaries : IDisposable
    {
        private readonly SqliteConnection connection;
        private readonly SqliteCommand executer;
        private static string? connection_string;

        public Dictionaries(SqliteConnection connection, SqliteCommand executer)
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

        public static Dictionaries Open()
        {
            if (connection_string == null)
                throw new Exception("The configuration is not set.");

            var connection = new SqliteConnection(connection_string);
            var executer = connection.CreateCommand();

            connection.Open();

            return new Dictionaries(connection, executer);
        }

        public long EnglishCount(string[] words)
        {
            var word_set = string.Join("','", words);
            executer.CommandText = string.Format(Q_CONTAINS, word_set);
            
            using var reader = executer.ExecuteReader();
            if (reader.Read()) return (long)reader[0];

            return 0;
        }

        public void Dispose()
        {
            connection.Dispose();
            executer.Dispose();
            GC.SuppressFinalize(this);
        }

        private const string Q_CONTAINS = @"
SELECT COUNT(DISTINCT Word) FROM en_US WHERE Word IN ('{0}')";
    }
}