using System.Text.Json;

namespace Photon.JobSeeker;

public sealed class AiVerdictUpdate
{
    public int AiScore { get; set; }

    public AiVerdict AiVerdict { get; set; }

    public string? AiReason { get; set; }

    public AiSeniority? AiSeniority { get; set; }

    public int? AiSalaryMin { get; set; }

    public int? AiSalaryMax { get; set; }

    public string? AiCurrency { get; set; }

    public AiPeriod? AiPeriod { get; set; }

    public AiWorkModel? AiWorkModel { get; set; }

    public AiContract? AiContract { get; set; }

    public int? AiExperienceYears { get; set; }

    public List<string>? AiSkills { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public JsonElement? Delta { get; set; }

    public string? TailoringNote { get; set; }
}
