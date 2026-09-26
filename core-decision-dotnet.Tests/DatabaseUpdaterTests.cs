using System.Data.SQLite;
using Photon.JobSeeker;

namespace Photon.JobSeeker.Tests;

public class DatabaseUpdaterTests : IDisposable
{
    private readonly SQLiteConnection keeper;
    private readonly Database database;
    private readonly string root;

    public DatabaseUpdaterTests()
    {
        root = Path.Combine(Path.GetTempPath(), "db-updater-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        keeper = new SQLiteConnection($"Data Source={Path.Combine(root, "data.sqlite3")};Pooling=False");
        keeper.Open();
        database = new Database(keeper);
    }

    public void Dispose()
    {
        database.Dispose();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Scripts_apply_in_name_order_and_are_recorded()
    {
        Write("002-second.sql", "create table Second (X integer); insert into Second values (2);");
        Write("001-first.sql", "create table First (X integer); insert into First values (1);");

        DatabaseUpdater.Run(database, root);

        Assert.Equal(1, database.ExecuteScalar<long>("select X from First"));
        Assert.Equal(2, database.ExecuteScalar<long>("select X from Second"));
        Assert.Equal(2, database.ExecuteScalar<long>("select count(*) from SchemaUpdate"));
    }

    [Fact]
    public void Already_applied_scripts_are_skipped_on_rerun()
    {
        var path = Write("001-once.sql", "create table Once (X integer); insert into Once values (1);");

        DatabaseUpdater.Run(database, root);

        File.WriteAllText(path, "insert into Once values (99);");

        DatabaseUpdater.Run(database, root);

        Assert.Equal(1, database.ExecuteScalar<long>("select count(*) from SchemaUpdate"));
        Assert.Equal(1, database.ExecuteScalar<long>("select count(*) from Once"));
        Assert.Equal(1, database.ExecuteScalar<long>("select X from Once"));
    }

    [Fact]
    public void Missing_folder_is_a_no_op()
    {
        var missing = Path.Combine(root, "missing");

        DatabaseUpdater.Run(database, missing);

        Assert.Equal(0, database.ExecuteScalar<long>(
            "select count(*) from sqlite_master where name = 'SchemaUpdate'"));
    }

    [Fact]
    public void Failing_script_rolls_back_and_throws()
    {
        Write("001-bad.sql", "create table Bad (X integer not null); insert into Bad values (NULL);");

        Assert.ThrowsAny<SQLiteException>(() => DatabaseUpdater.Run(database, root));

        Assert.Equal(0, database.ExecuteScalar<long>("select count(*) from SchemaUpdate"));
        Assert.Equal(0, database.ExecuteScalar<long>("select count(*) from sqlite_master where name = 'Bad'"));
    }

    [Fact]
    public void Files_after_a_failing_one_are_not_applied()
    {
        Write("001-bad.sql", "create table Bad (X integer not null); insert into Bad values (NULL);");
        Write("002-after.sql", "create table After (X integer);");

        Assert.ThrowsAny<SQLiteException>(() => DatabaseUpdater.Run(database, root));

        Assert.Equal(0, database.ExecuteScalar<long>("select count(*) from sqlite_master where name = 'After'"));
    }

    private string Write(string name, string sql)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, sql);
        return path;
    }
}
