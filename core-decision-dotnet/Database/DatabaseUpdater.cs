using Serilog;

namespace Photon.JobSeeker;

internal static class DatabaseUpdater
{
    public static void Run(Database database, string folder)
    {
        if (!Directory.Exists(folder))
        {
            Log.Debug("DatabaseUpdater: folder {Folder} does not exist - nothing to apply", folder);
            return;
        }

        database.Execute(@"
create table if not exists SchemaUpdate (
    Name        text        not null    primary key,
    AppliedAt   timestamp   not null    default current_timestamp
)");

        var applied = database.Query<string>("select Name from SchemaUpdate")
                              .ToHashSet(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(folder, "*.sql")
                                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);

            if (applied.Contains(name))
            {
                Log.Information("DatabaseUpdater: {Name} already applied", name);
                continue;
            }

            var script = File.ReadAllText(file);

            database.BeginTransaction(immediate: true);

            try
            {
                database.Execute(script);
                database.Execute("insert into SchemaUpdate (Name) values (@name)", new { name });
                database.Commit();
            }
            catch (Exception ex)
            {
                database.Rollback();
                Log.Error(ex, "DatabaseUpdater: {Name} failed and was rolled back", name);
                throw;
            }

            Log.Information("DatabaseUpdater: applied {Name}", name);
        }
    }
}
