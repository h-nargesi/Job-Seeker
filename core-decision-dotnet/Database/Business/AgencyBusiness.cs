using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Photon.JobSeeker;

class AgencyBusiness
{
    private readonly Database database;

    public AgencyBusiness(Database database) => this.database = database;

    public List<AgencyRate> JobRateReport()
    {
        return database.Query<AgencyRate>(Q_JOB_RATE_REPORT).ToList();
    }

    public void SaveState(Agency agency)
    {
        var settings = database.ExecuteScalar<string?>(Q_LOAD_SETTING, new { agency = agency.ID });
        if (settings == null) return;

        settings = Regex.Replace(settings, @"(""running"":)\s*\d+,", @$"$1 {agency.CurrentMethodIndex},");

        SaveSettings(agency.ID, settings, (long)agency.Status);
    }

    public void SaveSettings(long id, string settings, long active)
    {
        database.Execute(Q_UPDATE_SETTINGS, new { settings, active, id });
    }

    public AgencyInfo? LoadByName(string name)
    {
        var row = database.Query<AgencyRow>(Q_LOAD_BY_NAME, new { title = name }).FirstOrDefault();
        if (row == null) return null;

        return new AgencyInfo(
            row.AgencyID,
            row.Domain,
            row.Link,
            row.Active,
            row.Settings == null ? null : JsonConvert.DeserializeObject<Agency.AgencySetting>(row.Settings));
    }

    public (string user, string pass) GetUserPass(string agency)
    {
        var row = database.Query<CredentialRow>(Q_GET_USER_PASS, new { title = agency }).FirstOrDefault();
        if (row == null) return default;

        var password = row.Password;
        if (SecretProtector.LooksEncrypted(password)) password = SecretProtector.Decrypt(password);

        return (row.UserName, password);
    }

    public static void MigratePlaintextPasswords(Database database)
    {
        if (!SecretProtector.IsReady) return;

        var plaintext_agencies = database.Query<PasswordRow>(Q_GET_ALL_PASSWORDS)
            .Where(r => !string.IsNullOrEmpty(r.Password) && !SecretProtector.LooksEncrypted(r.Password))
            .Select(r => (r.AgencyID, Password: r.Password!))
            .ToList();

        foreach (var (id, password) in plaintext_agencies)
            database.Execute(Q_UPDATE_PASSWORD, new { pass = SecretProtector.Encrypt(password), agency = id });

        if (plaintext_agencies.Count > 0)
            Serilog.Log.Information("Encrypted {0} plaintext agency password(s).", plaintext_agencies.Count);
    }

    private sealed class AgencyRow
    {
        public long AgencyID { get; set; }

        public string Domain { get; set; } = string.Empty;

        public string Link { get; set; } = string.Empty;

        public long Active { get; set; }

        public string? Settings { get; set; }
    }

    private sealed class CredentialRow
    {
        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    private sealed class PasswordRow
    {
        public long AgencyID { get; set; }

        public string? Password { get; set; }
    }

    private readonly static string Q_JOB_RATE_REPORT = @$"
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

    ) job ON agc.AgencyID = job.AgencyID

) rate";

    private const string Q_LOAD_SETTING = @"
SELECT Settings FROM Agency WHERE AgencyID = @agency";

    private const string Q_LOAD_BY_NAME = @"
SELECT AgencyID, Domain, Link, Active, Settings FROM Agency WHERE Title = @title";

    private const string Q_GET_USER_PASS = @"
SELECT UserName, Password FROM Agency WHERE Title = @title";

    private const string Q_GET_ALL_PASSWORDS = @"
SELECT AgencyID, Password FROM Agency";

    private const string Q_UPDATE_PASSWORD = @"
UPDATE Agency SET Password = @pass WHERE AgencyID = @agency";

    private const string Q_UPDATE_SETTINGS = @"
UPDATE Agency SET Settings = @settings, Active = @active WHERE AgencyID = @id";
}
