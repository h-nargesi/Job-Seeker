using System.Globalization;

namespace Photon.JobSeeker;

class AppSettingBusiness
{
    public const string FloorKey = "floor";
    public const string AiPassmarkKey = "aipassmark";
    public const string ScoreCapKey = "scorecap";
    public const string WRegexKey = "w_regex";
    public const string WAiKey = "w_ai";
    public const string MemoryCapKey = "memorycap";
    public const string RemoteHybridKey = "remotehybrid";

    public const int FloorDefault = 70;
    public const int AiPassmarkDefault = 60;
    public const int ScoreCapDefault = 300;
    public const double WRegexDefault = 0.35;
    public const double WAiDefault = 0.65;
    public const int MemoryCapDefault = 500;
    public const int RemoteHybridDefault = 0;

    public static AppSettingField[] Fields { get; } =
    [
        new(FloorKey, "Eligibility floor", AppSettingField.IntKind,
            FloorDefault.ToString(CultureInfo.InvariantCulture)),
        new(AiPassmarkKey, "AI passmark", AppSettingField.IntKind,
            AiPassmarkDefault.ToString(CultureInfo.InvariantCulture)),
        new(ScoreCapKey, "Score cap", AppSettingField.IntKind,
            ScoreCapDefault.ToString(CultureInfo.InvariantCulture)),
        new(WRegexKey, "Regex weight", AppSettingField.DoubleKind,
            WRegexDefault.ToString(CultureInfo.InvariantCulture)),
        new(WAiKey, "AI weight", AppSettingField.DoubleKind,
            WAiDefault.ToString(CultureInfo.InvariantCulture)),
        new(MemoryCapKey, "Memory injection cap", AppSettingField.IntKind,
            MemoryCapDefault.ToString(CultureInfo.InvariantCulture)),
        new(RemoteHybridKey, "Remote/hybrid", AppSettingField.FlagKind,
            RemoteHybridDefault.ToString(CultureInfo.InvariantCulture)),
    ];

    private readonly Database database;

    public AppSettingBusiness(Database database) => this.database = database;

    public int Floor() => ReadInt(FloorKey, FloorDefault);

    public int AiPassmark() => ReadInt(AiPassmarkKey, AiPassmarkDefault);

    public int ScoreCap() => ReadInt(ScoreCapKey, ScoreCapDefault);

    public double WRegex() => ReadDouble(WRegexKey, WRegexDefault);

    public double WAi() => ReadDouble(WAiKey, WAiDefault);

    public int MemoryCap() => ReadInt(MemoryCapKey, MemoryCapDefault);

    public int RemoteHybrid() => ReadInt(RemoteHybridKey, RemoteHybridDefault);

    public Dictionary<string, string> FetchAll()
    {
        return database.Query<SettingRow>(Q_FETCH_ALL)
            .ToDictionary(r => r.Key, r => r.Value);
    }

    public void Save(string? key, string? value)
    {
        var field = Fields.FirstOrDefault(f => f.Key == key)
            ?? throw new BadJobRequest($"Unknown setting key: {key}");

        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new BadJobRequest($"Value for {field.Key} is empty");

        if (!Parses(field, trimmed))
            throw new BadJobRequest($"Value for {field.Key} must be a number: {trimmed}");

        database.Execute(Q_SAVE, new { key = field.Key, value = trimmed });
    }

    private static bool Parses(AppSettingField field, string value)
    {
        if (field.Kind == AppSettingField.DoubleKind)
            return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out _);

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private string? ReadValue(string key)
    {
        return database.ExecuteScalar<string?>(Q_GET, new { key });
    }

    private int ReadInt(string key, int fallback)
    {
        var raw = ReadValue(key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private double ReadDouble(string key, double fallback)
    {
        var raw = ReadValue(key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        return double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private sealed class SettingRow
    {
        public string Key { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }

    private const string Q_GET = @"
SELECT Value FROM AppSetting WHERE Key = @key";

    private const string Q_FETCH_ALL = @"
SELECT Key, Value FROM AppSetting";

    private const string Q_SAVE = @"
INSERT INTO AppSetting (Key, Value) VALUES (@key, @value)
ON CONFLICT(Key) DO UPDATE SET Value = @value";
}
