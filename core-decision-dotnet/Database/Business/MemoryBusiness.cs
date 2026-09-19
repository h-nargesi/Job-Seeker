namespace Photon.JobSeeker;

class MemoryBusiness
{
    private readonly Database database;

    public MemoryBusiness(Database database) => this.database = database;

    public long Insert(MemoryRow row)
    {
        var now = DateTime.Now;
        database.Execute(Q_INSERT, new
        {
            scope = row.Scope.ToString(),
            domain = NormalizeDomain(row.AgencyDomain),
            fieldKey = row.FieldKey.Trim(),
            fieldLabel = row.FieldLabel,
            kind = row.Kind.ToString(),
            confirmed = row.Confirmed,
            value = row.Value,
            note = row.Note,
            now,
        });
        return database.LastInsertRowId();
    }

    public MemoryRow? Fetch(long id)
    {
        return database.Query<MemoryRow>(Q_FETCH, new { id }).FirstOrDefault();
    }

    public List<MemoryRow> List(MemoryScope? scope = null, bool? confirmed = null)
    {
        return database.Query<MemoryRow>(Q_LIST, new
        {
            scope = scope?.ToString(),
            confirmed,
        }).ToList();
    }

    public bool Update(MemoryRow row)
    {
        database.Execute(Q_UPDATE, new
        {
            scope = row.Scope.ToString(),
            domain = NormalizeDomain(row.AgencyDomain),
            fieldKey = row.FieldKey.Trim(),
            fieldLabel = row.FieldLabel,
            kind = row.Kind.ToString(),
            confirmed = row.Confirmed,
            value = row.Value,
            note = row.Note,
            now = DateTime.Now,
            id = row.MemoryID,
        });
        return database.Changes() == 1;
    }

    public bool SetConfirmed(long id, bool confirmed)
    {
        database.Execute(Q_CONFIRM, new { confirmed, now = DateTime.Now, id });
        return database.Changes() == 1;
    }

    public bool Delete(long id)
    {
        database.Execute(Q_DELETE, new { id });
        return database.Changes() == 1;
    }

    public bool BumpUseCount(long id)
    {
        database.Execute(Q_BUMP, new { now = DateTime.Now, id });
        return database.Changes() == 1;
    }

    public List<MemoryRow> Snapshot(MemoryScope scope, int cap)
    {
        return database.Query<MemoryRow>(Q_SNAPSHOT, new { scope = scope.ToString(), cap }).ToList();
    }

    public List<MemoryRow> Query(MemoryScope scope, string domain, string fieldKey)
    {
        return database.Query<MemoryRow>(Q_QUERY, new
        {
            scope = scope.ToString(),
            domain = NormalizeDomain(domain),
            fieldKey = fieldKey.Trim(),
        }).ToList();
    }

    internal static string NormalizeDomain(string? domain)
    {
        var trimmed = domain?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(trimmed) ? "*" : trimmed;
    }

    private const string Q_PRECEDENCE = $@"
(Kind = '{nameof(MemoryKind.Correction)}') DESC,
(AgencyDomain = '*') ASC,
UseCount DESC,
UpdatedAt DESC,
MemoryID ASC";

    private const string Q_INSERT = @"
INSERT INTO Memory (Scope, AgencyDomain, FieldKey, FieldLabel, Kind, Confirmed, Value, Note, UseCount, CreatedAt, UpdatedAt)
VALUES (@scope, @domain, @fieldKey, @fieldLabel, @kind, @confirmed, @value, @note, 0, @now, @now)";

    private const string Q_FETCH = @"
SELECT * FROM Memory WHERE MemoryID = @id";

    private const string Q_LIST = @"
SELECT * FROM Memory
WHERE (@scope IS NULL OR Scope = @scope)
  AND (@confirmed IS NULL OR Confirmed = @confirmed)
ORDER BY MemoryID DESC";

    private const string Q_UPDATE = @"
UPDATE Memory SET
    Scope = @scope,
    AgencyDomain = @domain,
    FieldKey = @fieldKey,
    FieldLabel = @fieldLabel,
    Kind = @kind,
    Confirmed = @confirmed,
    Value = @value,
    Note = @note,
    UpdatedAt = @now
WHERE MemoryID = @id";

    private const string Q_CONFIRM = @"
UPDATE Memory SET Confirmed = @confirmed, UpdatedAt = @now WHERE MemoryID = @id";

    private const string Q_DELETE = @"
DELETE FROM Memory WHERE MemoryID = @id";

    private const string Q_BUMP = @"
UPDATE Memory SET UseCount = UseCount + 1, UpdatedAt = @now WHERE MemoryID = @id";

    private const string Q_SNAPSHOT = $@"
SELECT * FROM Memory
WHERE Scope = @scope AND Confirmed = 1
ORDER BY {Q_PRECEDENCE}
LIMIT @cap";

    private const string Q_QUERY = $@"
SELECT * FROM Memory
WHERE Scope = @scope AND Confirmed = 1 AND FieldKey = @fieldKey
  AND AgencyDomain IN (@domain, '*')
ORDER BY {Q_PRECEDENCE}";
}
