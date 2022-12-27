using Microsoft.Data.Sqlite;

namespace Photon.JobSeeker
{
    class TrendBusiness
    {
        private Database database;
        public TrendBusiness(Database database) => this.database = database;

        public Trend? GetByID(long id)
        {
            using var reader = database.Read(Q_GET, id);

            if (reader.Read()) return null;

            return ReadTrend(reader);
        }

        public List<object> Report()
        {
            using var reader = database.Read(Q_REPORT);
            var list = new List<object>();

            while (reader.Read())
                list.Add(new
                {
                    TrendID = reader["TrendID"],
                    Agency = reader["Agency"],
                    Link = reader["Link"],
                    LastActivity = reader["LastActivity"] ,
                    Type = reader["Type"],
                });

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
                database.Insert(nameof(Trend), model, filter);

                if (trend != null)
                    trend.TrendID = database.LastInsertRowId();
            }
            else database.Update(nameof(Trend), model, id, filter);
        }

        public void DeleteExpired(double minutes)
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
                Type = Enum.Parse<TrendType>((string)reader[nameof(Trend.Type)]),
            };
        }

        private const string Q_INDEX = @"
SELECT * FROM Trend";

        private const string Q_REPORT = @"
SELECT t.TrendID, a.Title AS Agency, a.Link, STRFTIME('%Y-%m-%d %H:%M:%s', t.LastActivity) AS LastActivity, t.Type
FROM Trend t JOIN Agency a ON t.AgencyID = a.AgencyID";

        private const string Q_GET = Q_INDEX + @"
WHERE TrendID = $trend";

        private const string Q_DELETE_EXPIRED = @"
DELETE FROM Trend WHERE DATETIME(LastActivity) <= $expiration";

    }
}