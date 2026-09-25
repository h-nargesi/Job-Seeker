using System.Data.SQLite;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Photon.JobSeeker
{
    class JobOptionBusiness
    {
        private readonly Database database;

        public JobOptionBusiness(Database database) => this.database = database;

        public JobOption[] FetchAll()
        {
            return database.Query<OptionRow>(Q_FETCH_ALL)
                .Select(r => new JobOption()
                {
                    Category = r.Category,
                    Score = r.Score,
                    Title = r.Title,
                    Pattern = new Regex(r.Pattern, RegexOptions.IgnoreCase),
                    Settings = r.Settings == null ? null : JsonConvert.DeserializeObject<JobOptionSettings>(r.Settings),
                })
                .ToArray();
        }

        public List<OptionEditRow> FetchRows()
        {
            return database.Query<OptionEditRow>(Q_FETCH_ROWS).ToList();
        }

        public long Save(OptionEditRow row)
        {
            row.Category = row.Category.Trim();
            row.Title = row.Title.Trim();
            row.Pattern = row.Pattern.Trim();

            if (row.Category.Length == 0) throw new BadJobRequest("Category is required");
            if (row.Title.Length == 0) throw new BadJobRequest("Title is required");
            if (row.Pattern.Length == 0) throw new BadJobRequest("Pattern is required");

            ValidatePattern(row.Pattern);
            row.Settings = NormalizeSettings(row.Settings);

            if (row.JobOptionID == 0)
            {
                try
                {
                    database.Execute(Q_INSERT, row);
                    return database.LastInsertRowId();
                }
                catch (SQLiteException ex) when (ex.Message.Contains("UNIQUE"))
                {
                    throw new BadJobRequest($"Title already exists: {row.Title}");
                }
            }

            if (database.Execute(Q_UPDATE, row) == 0)
                throw new BadJobRequest($"Option not found: {row.JobOptionID}");

            return row.JobOptionID;
        }

        public bool Delete(long joboptionid)
        {
            return database.Execute(Q_DELETE, new { joboptionid }) > 0;
        }

        public bool SetEffective(long joboptionid, bool effective)
        {
            return database.Execute(Q_SET_EFFECTIVE, new { joboptionid, effective }) > 0;
        }

        private static void ValidatePattern(string pattern)
        {
            try
            {
                _ = new Regex(pattern, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException ex)
            {
                throw new BadJobRequest($"Invalid pattern: {ex.Message}");
            }
        }

        private static string? NormalizeSettings(string? settings)
        {
            if (settings == null) return null;

            var trimmed = settings.Trim();
            if (trimmed.Length == 0) return null;

            try
            {
                JsonConvert.DeserializeObject<JobOptionSettings>(trimmed);
            }
            catch (JsonException ex)
            {
                throw new BadJobRequest($"Invalid settings JSON: {ex.Message}");
            }

            return trimmed;
        }

        private sealed class OptionRow
        {
            public string Category { get; set; } = string.Empty;

            public long Score { get; set; }

            public string Title { get; set; } = string.Empty;

            public string Pattern { get; set; } = string.Empty;

            public string? Settings { get; set; }
        }

        private const string Q_FETCH_ALL = @"
SELECT Category, Score, Title, Pattern, Settings FROM JobOption WHERE Efective != 0";

        private const string Q_FETCH_ROWS = @"
SELECT JobOptionID, Efective, Category, Score, Title, Pattern, Settings
FROM JobOption ORDER BY Category, Score DESC, Title";

        private const string Q_INSERT = @"
INSERT INTO JobOption (Efective, Category, Score, Title, Pattern, Settings)
VALUES (@Efective, @Category, @Score, @Title, @Pattern, @Settings)";

        private const string Q_UPDATE = @"
UPDATE JobOption SET Efective = @Efective, Category = @Category, Score = @Score,
Title = @Title, Pattern = @Pattern, Settings = @Settings
WHERE JobOptionID = @JobOptionID";

        private const string Q_DELETE = @"
DELETE FROM JobOption WHERE JobOptionID = @joboptionid";

        private const string Q_SET_EFFECTIVE = @"
UPDATE JobOption SET Efective = @effective WHERE JobOptionID = @joboptionid";
    }
}
