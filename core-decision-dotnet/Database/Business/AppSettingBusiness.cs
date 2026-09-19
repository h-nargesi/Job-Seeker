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

    public const int FloorDefault = 70;
    public const int AiPassmarkDefault = 60;
    public const int ScoreCapDefault = 300;
    public const double WRegexDefault = 0.35;
    public const double WAiDefault = 0.65;
    public const int MemoryCapDefault = 500;

    private readonly Database database;

    public AppSettingBusiness(Database database) => this.database = database;

    public int Floor() => ReadInt(FloorKey, FloorDefault);

    public int AiPassmark() => ReadInt(AiPassmarkKey, AiPassmarkDefault);

    public int ScoreCap() => ReadInt(ScoreCapKey, ScoreCapDefault);

    public double WRegex() => ReadDouble(WRegexKey, WRegexDefault);

    public double WAi() => ReadDouble(WAiKey, WAiDefault);

    public int MemoryCap() => ReadInt(MemoryCapKey, MemoryCapDefault);

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

    private const string Q_GET = @"
SELECT Value FROM AppSetting WHERE Key = @key";
}
