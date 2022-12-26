using System.Text.RegularExpressions;

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
            {
                option_list.Add(new JobOption()
                {
                    Title = (string)reader["Title"],
                    Score = (long)reader["Score"],
                    Pattern = new Regex((string)reader["Pattern"]),
                    Options = (string)reader["Options"],
                });
            }

            return option_list.ToArray();
        }

        private const string Q_FETCH_ALL = @"
SELECT Score, Title, Pattern, Options FROM JobOption WHERE Efective != 0";
    }
}