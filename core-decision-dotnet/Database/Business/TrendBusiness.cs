﻿using System.Data.SQLite;

namespace Photon.JobSeeker
{
    class TrendBusiness : BaseBusiness<Trend>
    {
        public const int TREND_EXPIRATION_MINUTES = 2;
        public TrendBusiness(Database database) : base(database) { }

        protected override string[]? GetUniqueColumns { get; } = new string[] {
            nameof(TrendFilter.AgencyID), nameof(TrendFilter.Type)
        };

        public Trend? Get(long agency_id, TrendType type)
        {
            using var reader = database.Read(Q_GET, agency_id, type);

            if (!reader.Read()) return null;

            return ReadTrend(reader);
        }

        public List<dynamic> Report()
        {
            using var reader = database.Read(Q_REPORT);
            var list = new List<dynamic>();

            while (reader.Read())
                list.Add(new
                {
                    TrendID = reader["TrendID"] as long?,
                    Agency = reader["Agency"] as string ?? "None",
                    Link = reader["Link"] as string ?? "",
                    LastActivity = reader["LastActivity"] as string ?? "-",
                    Type = reader["Type"] as string,
                    State = reader["State"] as string,
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

        public void Block(long agency_id, TrendType type)
        {
            Save(new
            {
                AgencyID = agency_id,
                Type = type,
                State = TrendState.Blocked,
            });
        }

        public void Block(long trend_id)
        {
            Save(new
            {
                TrendID = trend_id,
                State = TrendState.Blocked,
            });
        }

        public void ClearSearching(long agency_id)
        {
            database.Execute(Q_DELETE_AGENCY, agency_id);
        }

        private static Trend ReadTrend(SQLiteDataReader reader)
        {
            return new Trend
            {
                TrendID = (long)reader[nameof(Trend.TrendID)],
                AgencyID = (long)reader[nameof(Trend.AgencyID)],
                LastActivity = (DateTime)reader[nameof(Trend.LastActivity)],
                State = Enum.Parse<TrendState>((string)reader[nameof(Trend.State)]),
                Reserved = (bool)reader[nameof(Trend.Reserved)],
            };
        }

        private const string Q_INDEX = @"
SELECT * FROM Trend";

        private const string Q_REPORT = @$"
SELECT a.Title AS Agency, a.Link, t.TrendID, t.Type
    , CASE a.Active WHEN 0 THEN '{nameof(TrendState.Blocked)}' ELSE t.State END AS State
    , STRFTIME('%Y-%m-%d %H:%M:%S', t.LastActivity) AS LastActivity
FROM Agency a LEFT JOIN Trend t ON t.AgencyID = a.AgencyID";

        private const string Q_GET = Q_INDEX + @"
WHERE AgencyID = $agency AND Type = $type";

        private const string Q_DELETE_EXPIRED = @"
DELETE FROM Trend WHERE DATETIME(LastActivity) <= $expiration";

        private const string Q_DELETE_AGENCY = @$"
DELETE FROM Trend WHERE AgencyID = $agencyid AND Type = '{nameof(TrendType.Search)}'";

    }
}