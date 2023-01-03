using Newtonsoft.Json;

namespace Photon.JobSeeker
{
    class AgencyBusiness
    {
        private readonly Database database;
        public AgencyBusiness(Database database) => this.database = database;

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

        private const string Q_LOAD_SETTING = @"
SELECT Settings FROM Agency WHERE AgencyID = $agency";

        private const string Q_LOAD_BY_NAME = @"
SELECT AgencyID, Domain, Link, Active, Settings FROM Agency WHERE Title = $title AND Active != 0";

        private const string Q_GET_USER_PASS = @"
SELECT UserName, Password FROM Agency WHERE Title = $title";
    }
}