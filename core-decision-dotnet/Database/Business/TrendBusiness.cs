using Dapper;

namespace Photon.JobSeeker
{
    class TrendBusiness
    {
        public const int TREND_EXPIRATION_MINUTES = 2;

        private readonly Database database;

        public TrendBusiness(Database database) => this.database = database;

        public Trend? Get(long agency_id, TrendType type)
        {
            return database.Query<Trend>(Q_GET, new { agency = agency_id, type = type.ToString() }).FirstOrDefault();
        }

        public List<dynamic> Report()
        {
            return database.Query<ReportRow>(Q_REPORT)
                .Select(r => (dynamic)new
                {
                    TrendID = r.TrendID,
                    Agency = r.Agency ?? "None",
                    Link = r.Link ?? "",
                    LastActivity = r.LastActivity ?? "-",
                    Type = r.Type,
                    State = r.State,
                })
                .ToList();
        }

        public List<Trend> FetchAll()
        {
            return database.Query<Trend>(Q_INDEX).ToList();
        }

        public void CreateTrend(Trend trend)
        {
            database.Execute(Q_INSERT, new
            {
                agencyId = trend.AgencyID,
                type = trend.Type.ToString(),
                state = trend.State.ToString(),
                lastActivity = trend.LastActivity,
                reserved = trend.Reserved,
            });

            if (database.Changes() == 1)
                trend.TrendID = database.LastInsertRowId();
            else
                trend.TrendID = Get(trend.AgencyID, trend.Type)?.TrendID ?? 0;
        }

        public void UpdateActivity(Trend trend)
        {
            database.Execute(Q_UPDATE_ACTIVITY, new
            {
                trendId = trend.TrendID,
                state = trend.State.ToString(),
                type = trend.Type.ToString(),
                lastActivity = trend.LastActivity,
                reserved = trend.Reserved,
            });
        }

        public void DeleteExpired(double minutes = TREND_EXPIRATION_MINUTES)
        {
            database.Execute(Q_DELETE_EXPIRED, new { expiration = DateTime.Now.AddMinutes(-minutes) });
        }

        public long Block(long agency_id, TrendType type)
        {
            database.Execute(Q_BLOCK, new { agencyId = agency_id, type = type.ToString() });

            if (database.Changes() == 1)
                return database.LastInsertRowId();

            return Get(agency_id, type)?.TrendID ?? 0;
        }

        public void Block(long trend_id)
        {
            database.Execute(Q_BLOCK_BY_ID, new { trendId = trend_id });
        }

        public void ClearSearching(long agency_id)
        {
            database.Execute(Q_DELETE_AGENCY, new { agencyid = agency_id });
        }

        public void Delete(long id)
        {
            database.Execute(Q_DELETE, new { trendId = id });
        }

        private sealed class ReportRow
        {
            public long? TrendID { get; set; }

            public string? Agency { get; set; }

            public string? Link { get; set; }

            public string? LastActivity { get; set; }

            public string? Type { get; set; }

            public string? State { get; set; }
        }

        private const string Q_INDEX = @"
SELECT * FROM Trend";

        private readonly static string Q_REPORT = @$"
SELECT a.Title AS Agency, a.Link, t.TrendID, t.Type
    , CASE a.Active WHEN 0 THEN '{nameof(TrendState.Blocked)}' ELSE t.State END AS State
    , STRFTIME('%Y-%m-%d %H:%M:%S', t.LastActivity) AS LastActivity
FROM Agency a LEFT JOIN Trend t ON t.AgencyID = a.AgencyID";

        private const string Q_GET = Q_INDEX + @"
WHERE AgencyID = @agency AND Type = @type";

        private const string Q_INSERT = @"
INSERT INTO Trend (AgencyID, Type, State, LastActivity, Reserved)
VALUES (@agencyId, @type, @state, @lastActivity, @reserved)
ON CONFLICT(AgencyID, Type) DO NOTHING;";

        private const string Q_UPDATE_ACTIVITY = @"
UPDATE Trend SET State = @state, Type = @type, LastActivity = @lastActivity, Reserved = @reserved
WHERE TrendID = @trendId";

        private const string Q_DELETE_EXPIRED = @"
DELETE FROM Trend WHERE DATETIME(LastActivity) <= @expiration";

        private readonly static string Q_DELETE_AGENCY = @$"
DELETE FROM Trend WHERE AgencyID = @agencyid AND Type = '{nameof(TrendType.Search)}'";

        private readonly static string Q_BLOCK = $@"
INSERT INTO Trend (AgencyID, Type, State)
VALUES (@agencyId, @type, '{nameof(TrendState.Blocked)}')
ON CONFLICT(AgencyID, Type) DO NOTHING;";

        private readonly static string Q_BLOCK_BY_ID = $@"
UPDATE Trend SET State = '{nameof(TrendState.Blocked)}'
WHERE TrendID = @trendId";

        private const string Q_DELETE = @"
DELETE FROM Trend WHERE TrendID = @trendId";
    }
}
