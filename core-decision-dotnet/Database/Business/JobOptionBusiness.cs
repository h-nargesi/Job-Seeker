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
            using var reader = database.Read(Q_FETCH_ALL);

            var option_list = new List<JobOption>();
            while (reader.Read())
                option_list.Add(new JobOption()
                {
                    Category = (string)reader[nameof(JobOption.Category)],
                    Score = (long)reader[nameof(JobOption.Score)],
                    Title = (string)reader[nameof(JobOption.Title)],
                    Pattern = new Regex((string)reader[nameof(JobOption.Pattern)], RegexOptions.IgnoreCase),
                    Settings = reader[nameof(JobOption.Settings)] is not string settings ? null : JsonConvert.DeserializeObject<dynamic>(settings),
                });

            return option_list.ToArray();
        }

        private const string Q_FETCH_ALL = @"
SELECT Category, Score, Title, Pattern, Settings FROM JobOption WHERE Efective != 0";
    }
}