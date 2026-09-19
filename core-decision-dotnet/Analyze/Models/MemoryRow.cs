namespace Photon.JobSeeker;

public enum MemoryScope
{
    Resume,
    Apply,
    Ranking,
}

public enum MemoryKind
{
    Tip,
    Correction,
}

public class MemoryRow
{
    public long MemoryID { get; set; }

    public MemoryScope Scope { get; set; }

    public string AgencyDomain { get; set; } = "*";

    public string FieldKey { get; set; } = string.Empty;

    public string? FieldLabel { get; set; }

    public MemoryKind Kind { get; set; }

    public bool Confirmed { get; set; }

    public string Value { get; set; } = string.Empty;

    public string? Note { get; set; }

    public long UseCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public static class MemoryRankingKeys
{
    public const string VisaSponsorship = "visa_sponsorship";
    public const string NoStaffing = "no_staffing";
    public const string RemoteOnly = "remote_only";
    public const string SalaryFloor = "salary_floor";
    public const string SeniorityFloor = "seniority_floor";
    public const string MustHaveLanguage = "must_have_language";
    public const string ContractType = "contract_type";
    public const string Relocation = "relocation";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        VisaSponsorship,
        NoStaffing,
        RemoteOnly,
        SalaryFloor,
        SeniorityFloor,
        MustHaveLanguage,
        ContractType,
        Relocation,
    };
}
