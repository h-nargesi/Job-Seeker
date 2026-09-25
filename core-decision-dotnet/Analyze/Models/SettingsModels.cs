namespace Photon.JobSeeker;

public sealed record AppSettingField(string Key, string Label, string Kind, string Default)
{
    public const string IntKind = "int";

    public const string DoubleKind = "double";

    public const string FlagKind = "flag";
}

public sealed class OptionEditRow
{
    public long JobOptionID { get; set; }

    public bool Efective { get; set; } = true;

    public string Category { get; set; } = string.Empty;

    public long Score { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Pattern { get; set; } = string.Empty;

    public string? Settings { get; set; }
}

public sealed record SettingsPageModel(
    AppSettingField[] Fields,
    Dictionary<string, string> Values,
    List<OptionEditRow> Options,
    List<AgencyWaitingItem> Agencies);

public sealed record AgencyWaitingItem(string Name, int? Waiting, int DefaultWaiting);
