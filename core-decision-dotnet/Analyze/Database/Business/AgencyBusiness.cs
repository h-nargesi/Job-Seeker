namespace Photon.JobSeeker
{
    class AgencyBusiness
    {
        private readonly Database database;
        public AgencyBusiness(Database database) => this.database = database;

        public (long id, string domain) LoadByName(string name)
        {
            using var reader = database.Read(Q_LOAD_BY_NAME, name);
            if (!reader.Read()) return default;

            return ((long)reader["AgencyID"], (string)reader["Domain"]);
        }

        public (string user, string pass) GetUserPass(string agency)
        {
            using var database = Database.Open();
            using var reader = database.Read(Q_GET_USER_PASS, agency);

            if (!reader.Read()) return default;
            else
            {
                return ((string)reader["UserName"], (string)reader["Password"]);
            }
        }

        private const string Q_LOAD_BY_NAME = "SELECT AgencyID, Domain FROM Agency WHERE Title = $title";

        private const string Q_GET_USER_PASS = "SELECT UserName, Password FROM Agency WHERE Title = $title";
    }
}