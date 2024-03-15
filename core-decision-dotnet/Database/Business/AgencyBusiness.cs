using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Photon.JobSeeker
{
    class AgencyBusiness
    {
        private readonly Database database;
        public AgencyBusiness(Database database) => this.database = database;

        public List<dynamic> JobRateReport()
        {
            using var reader = database.Read(Q_JOB_RATE_REPORT);
            var list = new List<dynamic>();

            while (reader.Read())
                list.Add(new
                {
                    AgencyID = (long)reader["AgencyID"],
                    Title = (string)reader["Title"],
                    JobCount = (long)reader["JobCount"],
                    Analyzed = (long)reader["Analyzed"],
                    Accepted = (long)reader["Accepted"],
                    Applied = (long)reader["Applied"],
                    AnalyzingRate = (long)reader["AnalyzingRate"],
                    AcceptingRate = (long)reader["AcceptingRate"],
                });

            return list;
        }

        public void SaveState(Agency agency)
        {
            string? settings;
            using (var reader = database.Read(Q_LOAD_SETTING, agency.ID))
            {
                if (!reader.Read()) return;
                settings = reader["Settings"] as string;
                if (settings == null) return;
            }

            settings = Regex.Replace(settings, @"(""running"":)\s*\d+,", @$"$1 {agency.RunningSearchingMethodIndex},");

            database.Update(
                nameof(Agency),
                new { Settings = settings, Active = (long)agency.Status },
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
SELECT rate.*
	, CASE JobCount WHEN 0 THEN 0 ELSE CAST(100 * CAST(Analyzed AS REAL) / JobCount AS INTEGER) END AS AnalyzingRate
	, CASE Analyzed WHEN 0 THEN 0 ELSE CAST(100 * CAST(Accepted AS REAL) / Analyzed AS INTEGER) END AS AcceptingRate
FROM (
    SELECT agc.AgencyID, agc.Title
        , IFNULL(job.JobCount, 0) AS JobCount
        , IFNULL(job.Analyzed, 0) AS Analyzed
        , IFNULL(job.Attention, 0) + IFNULL(job.Applied, 0) AS Accepted
        , IFNULL(job.Applied, 0) AS Applied
    FROM Agency agc
    LEFT JOIN  (
        SELECT AgencyID
            , COUNT(*) AS JobCount
            , SUM(CASE State WHEN '{nameof(JobState.Saved)}' THEN 0 ELSE 1 END) AS Analyzed
            , SUM(CASE State WHEN '{nameof(JobState.Attention)}' THEN 1 ELSE 0 END) AS Attention
            , SUM(CASE State WHEN '{nameof(JobState.Applied)}' THEN 1 ELSE 0 END) AS Applied
        FROM Job
        GROUP BY AgencyID
    
    ) job on agc.AgencyID = job.AgencyID

) rate";

        private const string Q_LOAD_SETTING = @"
SELECT Settings FROM Agency WHERE AgencyID = $agency";

        private const string Q_LOAD_BY_NAME = @"
SELECT AgencyID, Domain, Link, Active, Settings FROM Agency WHERE Title = $title";

        private const string Q_GET_USER_PASS = @"
SELECT UserName, Password FROM Agency WHERE Title = $title";
    }
}