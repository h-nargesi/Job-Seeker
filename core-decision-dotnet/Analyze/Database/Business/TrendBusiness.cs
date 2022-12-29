using Microsoft.Data.Sqlite;

namespace Photon.JobSeeker
{
    class TrendBusiness
    {
        public const int TREND_EXPIRATION_MINUTES = 5;
        private Database database;
        public TrendBusiness(Database database) => this.database = database;

        public Trend? Get(long agency_id, TrendType type)
        {
            using var reader = database.Read(Q_GET, agency_id, type);

            if (!reader.Read()) return null;

            return ReadTrend(reader);
        }

        public List<object> Report()
        {
            using var reader = database.Read(Q_REPORT);
            var list = new List<object>();

            while (reader.Read())
            {
                var state_str = reader["State"] as string;
                var state = state_str == null ? (TrendState?)null : Enum.Parse<TrendState>(state_str);
                list.Add(new
                {
                    TrendID = reader["TrendID"] as long?,
                    Agency = reader["Agency"] as string ?? "-",
                    Link = reader["Link"] as string ?? "-",
                    LastActivity = reader["LastActivity"] as string ?? "-",
                    Type = state?.GetTrendType().ToString(),
                    State = state_str,
                });
            }

            return list;
        }

        public List<Trend> FetchAll()
        {
            using var reader = database.Read(Q_INDEX);
            var list = new List<Trend>();

            while (reader.Read())
                list.Add(ReadTrend(reader));

            return list;
        }

        public void Save(object model, TrendFilter filter = TrendFilter.All)
        {
            long id;

            var trend = model as Trend;
            if (trend != null) id = trend.TrendID;
            else
            {
                var id_property = model.GetType().GetProperty(nameof(Trend.TrendID));
                if (id_property != null)
                    id = (long?)id_property.GetValue(model) ?? default;
                else id = default;
            }

            if (id == default)
            {
                database.Insert(nameof(Trend), model, filter,
                    "ON CONFLICT(AgencyID, Type) DO NOTHING;");

                if (trend != null)
                    trend.TrendID = database.LastInsertRowId();
            }
            else database.Update(nameof(Trend), model, id, filter);
        }

        public void DeleteExpired(double minutes = TREND_EXPIRATION_MINUTES)
        {
            database.Execute(Q_DELETE_EXPIRED, DateTime.Now.AddMinutes(-minutes));
        }

        private static Trend ReadTrend(SqliteDataReader reader)
        {
            return new Trend
            {
                TrendID = (long)reader[nameof(Trend.TrendID)],
                AgencyID = (long)reader[nameof(Trend.AgencyID)],
                LastActivity = DateTime.Parse((string)reader[nameof(Trend.LastActivity)]),
                State = Enum.Parse<TrendState>((string)reader[nameof(Trend.State)]),
            };
        }

        private const string Q_INDEX = @"
SELECT * FROM Trend";

        private const string Q_REPORT = @"
SELECT a.Title AS Agency, a.Link, t.TrendID, t.State
    , STRFTIME('%Y-%m-%d %H:%M:%S', t.LastActivity) AS LastActivity
FROM Agency a LEFT JOIN Trend t ON t.AgencyID = a.AgencyID";

        private const string Q_GET = Q_INDEX + @"
WHERE AgencyID = $agency AND Type = $type";

        private const string Q_DELETE_EXPIRED = @"
DELETE FROM Trend WHERE DATETIME(LastActivity) <= $expiration";

    }
}