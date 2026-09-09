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
    }
}
