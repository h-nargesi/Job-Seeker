using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Photon.JobSeeker
{
    class AgencyBusiness
    {
        private readonly Database database;
        public AgencyBusiness(Database database) => this.database = database;

        public Dictionary<long, object> JobRateReport()
        {
            using var reader = database.Read(Q_JOB_RATE_REPORT);
            var list = new Dictionary<long, object>();

            while (reader.Read())
            {
                var AgencyID = (long)reader["AgencyID"];
                list.Add(AgencyID, new
                {
                    Saved = (long)reader["Saved"],
                    Analyzed = (long)reader["Analyzed"],
                    Rate = (long)reader["Rate"],
                });
            }

            return list;
        }

        public void ChangeRunningMethod(Agency agency)
        {
            string? settings;
            using (var reader = database.Read(Q_LOAD_SETTING, agency.ID))
            {
                if (!reader.Read()) return;
                settings = reader["Settings"] as string;
                if (settings == null) return;
            }

            settings = Regex.Replace(settings, @"(""running"":)\s*\d+,", @$"$1 {agency.RunningMethodIndex},");
            database.Update(
                nameof(Agency),
                new { Settings = settings },
                agency.ID);
        }

        public dynamic? LoadSetting(long id)
        {
            using var reader = database.Read(Q_LOAD_SETTING, id);
            if (!reader.Read()) return null;

            return reader["Settings"] is not string settings ? null : JsonConvert.DeserializeObject<dynamic>(settings);
        }

        public dynamic? LoadByName(string name)
        {
            using var reader = database.Read(Q_LOAD_BY_NAME, name);
            if (!reader.Read()) return null;

            return new
            {
                AgencyID = (long)reader["AgencyID"],
                Domain = (string)reader["Domain"],
                Link = (string)reader["Link"],
                Active = (long)reader["Active"],
                Settings = reader["Settings"] is not string settings ? null : JsonConvert.DeserializeObject<dynamic>(settings)
            };
        }

        public static (string user, string pass) GetUserPass(string agency)
        {
            using var database = Database.Open();
            using var reader = database.Read(Q_GET_USER_PASS, agency);

            if (!reader.Read()) return default;
            else
            {
                return ((string)reader["UserName"], (string)reader["Password"]);
            }
        }

        private const string Q_JOB_RATE_REPORT = @$"
SELECT job.*
	, CAST(100 * CAST(Analyzed AS REAL) / Saved AS INTEGER) AS Rate
FROM (
	SELECT AgencyID
		, SUM(CASE State WHEN '{nameof(JobState.Saved)}' THEN 0 ELSE 1 END) AS Saved
		, SUM(CASE State WHEN '{nameof(JobState.Saved)}' THEN 1 ELSE 0 END) AS Analyzed
	FROM Job
	GROUP BY AgencyID
) job";

        private const string Q_LOAD_SETTING = @"
SELECT Settings FROM Agency WHERE AgencyID = $agency";

        private const string Q_LOAD_BY_NAME = @"
SELECT AgencyID, Domain, Link, Active, Settings FROM Agency WHERE Title = $title AND Active != 0";

        private const string Q_GET_USER_PASS = @"
SELECT UserName, Password FROM Agency WHERE Title = $title";
    }
}