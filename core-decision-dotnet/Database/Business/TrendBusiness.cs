using Dapper;

namespace Photon.JobSeeker
{
    class TrendBusiness
    {
        public const int TREND_EXPIRATION_MINUTES = 5;
        public const int AUTH_EXPIRATION_MINUTES = 10;
        public const int CHALLENGE_EXPIRATION_MINUTES = 30;
        public const int RESERVATION_LEASE_SECONDS = 30;

        public const string CHALLENGE_STATE = "Challenge";

        private readonly Database database;

        public TrendBusiness(Database database) => this.database = database;

        public Trend? Get(long agency_id, TrendType type)
        {
            return database.Query<Trend>(Q_GET, new { agency = agency_id, type = type.ToString() }).FirstOrDefault();
        }

        public Trend? GetById(long trend_id)
        {
            return database.Query<Trend>(Q_GET_BY_ID, new { trendId = trend_id }).FirstOrDefault();
        }

        public Trend? FindHoldable(long agency_id)
        {
            return database.Query<Trend>(Q_FIND_HOLDABLE,
                new { agency = agency_id, blocked = TrendType.Blocked.ToString() }).FirstOrDefault();
        }

        public Trend? FindChallenged(long agency_id)
        {
            return database.Query<Trend>(Q_FIND_CHALLENGED,
                new { agency = agency_id, blocked = TrendState.Blocked.ToString() }).FirstOrDefault();
        }

        public int ReleaseChallenges(long agency_id)
        {
            return database.Execute(Q_RELEASE_CHALLENGES, new { agencyId = agency_id });
        }

        public static void MigrateChallengeColumn(Database database)
        {
            var columns = database.ReadAll("PRAGMA table_info(Trend)");
            if (columns.Count == 0) return;

            if (columns.Any(c => c.TryGetValue("name", out var name) && name is string column_name && column_name == "Challenge"))
                return;

            database.Execute(Q_ADD_CHALLENGE_COLUMN);
        }

        public List<TrendReportItem> Report()
        {
            return database.Query<ReportRow>(Q_REPORT)
                .Select(r => new TrendReportItem(
                    r.TrendID,
                    r.Agency ?? "None",
                    r.Link ?? "",
                    r.LastActivity ?? "-",
                    r.Type,
                    r.State))
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
                challenge = trend.Challenge,
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

        public void MarkChallenge(long trend_id)
        {
            database.Execute(Q_MARK_CHALLENGE, new { trendId = trend_id, now = DateTime.Now });
        }

        public void DeleteExpired(double minutes = TREND_EXPIRATION_MINUTES)
        {
            var auth_minutes = minutes * AUTH_EXPIRATION_MINUTES / TREND_EXPIRATION_MINUTES;
            var challenge_minutes = minutes * CHALLENGE_EXPIRATION_MINUTES / TREND_EXPIRATION_MINUTES;

            database.Execute(Q_DELETE_EXPIRED, new { cutoff = DateTime.Now.AddMinutes(-minutes) });
            database.Execute(Q_DELETE_EXPIRED_AUTH, new { cutoff = DateTime.Now.AddMinutes(-auth_minutes) });
            database.Execute(Q_DELETE_EXPIRED_CHALLENGE, new { cutoff = DateTime.Now.AddMinutes(-challenge_minutes) });
        }

        public void DeleteExpiredReservations(double seconds = RESERVATION_LEASE_SECONDS)
        {
            database.Execute(Q_DELETE_EXPIRED_RESERVATIONS, new { cutoff = DateTime.Now.AddSeconds(-seconds) });
        }

        public bool Touch(long trendId)
        {
            database.Execute(Q_TOUCH, new { trendId, now = DateTime.Now });
            return database.Changes() == 1;
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

        public void Clear(long agency_id, TrendType type)
        {
            database.Execute(Q_DELETE_TYPE, new { agencyid = agency_id, type = type.ToString() });
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
    , CASE WHEN t.Challenge = 1 THEN '{CHALLENGE_STATE}'
           WHEN a.Active = 0 THEN '{nameof(TrendState.Blocked)}' ELSE t.State END AS State
    , STRFTIME('%Y-%m-%d %H:%M:%S', t.LastActivity) AS LastActivity
FROM Agency a LEFT JOIN Trend t ON t.AgencyID = a.AgencyID";

        private const string Q_GET = Q_INDEX + @"
WHERE AgencyID = @agency AND Type = @type";

        private const string Q_GET_BY_ID = Q_INDEX + @"
WHERE TrendID = @trendId";

        private readonly static string Q_FIND_HOLDABLE = Q_INDEX + @"
WHERE AgencyID = @agency AND Type != @blocked
ORDER BY LastActivity DESC LIMIT 1";

    private readonly static string Q_FIND_CHALLENGED = $@"
SELECT * FROM Trend
WHERE AgencyID = @agency AND Challenge = 1 AND State != '{nameof(TrendState.Blocked)}'
ORDER BY LastActivity DESC LIMIT 1";

    private const string Q_RELEASE_CHALLENGES = @"
UPDATE Trend SET Challenge = 0 WHERE AgencyID = @agencyId AND Challenge = 1";

        private const string Q_ADD_CHALLENGE_COLUMN = @"
ALTER TABLE Trend ADD COLUMN Challenge bit NOT NULL DEFAULT 0";

        private const string Q_INSERT = @"
INSERT INTO Trend (AgencyID, Type, State, LastActivity, Reserved, Challenge)
VALUES (@agencyId, @type, @state, @lastActivity, @reserved, @challenge)
ON CONFLICT(AgencyID, Type) DO NOTHING;";

        private const string Q_UPDATE_ACTIVITY = @"
UPDATE Trend SET State = @state, Type = @type, LastActivity = @lastActivity, Reserved = @reserved, Challenge = 0
WHERE TrendID = @trendId";

        private const string Q_MARK_CHALLENGE = @"
UPDATE Trend SET Challenge = 1, LastActivity = @now
WHERE TrendID = @trendId";

        private readonly static string Q_DELETE_EXPIRED = $@"
DELETE FROM Trend WHERE Reserved = 0 AND Challenge = 0 AND State != '{nameof(TrendState.Auth)}' AND DATETIME(LastActivity) <= @cutoff";

        private readonly static string Q_DELETE_EXPIRED_AUTH = $@"
DELETE FROM Trend WHERE Reserved = 0 AND Challenge = 0 AND State = '{nameof(TrendState.Auth)}' AND DATETIME(LastActivity) <= @cutoff";

        private readonly static string Q_DELETE_EXPIRED_CHALLENGE = $@"
DELETE FROM Trend WHERE Reserved = 0 AND Challenge = 1 AND DATETIME(LastActivity) <= @cutoff";

        private const string Q_DELETE_EXPIRED_RESERVATIONS = @"
DELETE FROM Trend WHERE Reserved = 1 AND DATETIME(LastActivity) <= @cutoff";

        private const string Q_TOUCH = @"
UPDATE Trend SET LastActivity = @now WHERE TrendID = @trendId";

        private readonly static string Q_DELETE_TYPE = @"
DELETE FROM Trend WHERE AgencyID = @agencyid AND Type = @type";

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
