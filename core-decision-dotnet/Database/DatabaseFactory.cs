using System.Data.SQLite;
using Dapper;
using Microsoft.Extensions.Configuration;

namespace Photon.JobSeeker;

public sealed class DatabaseFactory(IConfiguration configuration) : IDatabaseFactory
{
    private static bool wal_configured;

    public Database Open()
    {
        var path = configuration["Database:Path"]
            ?? throw new InvalidOperationException("Database:Path is not configured.");

        var connection = new SQLiteConnection($"Data Source={path};Foreign Keys=True");
        connection.Open();

        if (!wal_configured)
        {
            connection.Execute("PRAGMA journal_mode=WAL");
            wal_configured = true;
        }

        connection.Execute("PRAGMA busy_timeout=5000");

        return new Database(connection);
    }
}
