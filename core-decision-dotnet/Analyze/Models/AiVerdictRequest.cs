using System.Text.Json;
using System.Text.Json.Serialization;

namespace Photon.JobSeeker;

public sealed class AiVerdictRequest
{
    public const int ReasonMaxLength = 2000;
    public const int SkillsMaxCount = 20;
    public const int ExperienceMaxYears = 50;

    [JsonPropertyName("jobId")]
    public long? JobId { get; set; }

    [JsonPropertyName("relevance")]
    public int? Relevance { get; set; }

    [JsonPropertyName("verdict")]
    public string? Verdict { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("seniority")]
    public string? Seniority { get; set; }

    [JsonPropertyName("salary_min")]
    public int? SalaryMin { get; set; }

    [JsonPropertyName("salary_max")]
    public int? SalaryMax { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("period")]
    public string? Period { get; set; }

    [JsonPropertyName("work_model")]
    public string? WorkModel { get; set; }

    [JsonPropertyName("contract")]
    public string? Contract { get; set; }

    [JsonPropertyName("experience_years")]
    public int? ExperienceYears { get; set; }

    [JsonPropertyName("skills")]
    public List<string>? Skills { get; set; }

    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; set; }

    [JsonPropertyName("delta")]
    public JsonElement? Delta { get; set; }

    public bool TryCreate(out AiVerdictUpdate update, out string error)
    {
        update = new AiVerdictUpdate();
        error = string.Empty;

        if (Relevance is not int score || score < 0 || score > 100)
        {
            error = "relevance must be an integer 0-100";
            return false;
        }

        if (!TryEnum(Verdict, out AiVerdict verdict))
        {
            error = "verdict must be a valid AiVerdict name";
            return false;
        }

        if (Reason != null && Reason.Length > ReasonMaxLength)
        {
            error = $"reason must be <= {ReasonMaxLength} characters";
            return false;
        }

        if (Skills != null && Skills.Count > SkillsMaxCount)
        {
            error = $"skills must have <= {SkillsMaxCount} items";
            return false;
        }

        if (SalaryMin is < 0 || SalaryMax is < 0)
        {
            error = "salary_min and salary_max must be >= 0";
            return false;
        }

        if (ExperienceYears is int years && (years < 0 || years > ExperienceMaxYears))
        {
            error = $"experience_years must be 0-{ExperienceMaxYears}";
            return false;
        }

        if (string.IsNullOrEmpty(Fingerprint))
        {
            error = "fingerprint is required";
            return false;
        }

        if (!TryOptionalEnum(Seniority, out AiSeniority? seniority) ||
            !TryOptionalEnum(Period, out AiPeriod? period) ||
            !TryOptionalEnum(WorkModel, out AiWorkModel? work_model) ||
            !TryOptionalEnum(Contract, out AiContract? contract))
        {
            error = "extraction enums must use locked names";
            return false;
        }

        update.AiScore = score;
        update.AiVerdict = verdict;
        update.AiReason = Reason;
        update.AiSeniority = seniority;
        update.AiSalaryMin = SalaryMin;
        update.AiSalaryMax = SalaryMax;
        update.AiCurrency = Currency;
        update.AiPeriod = period;
        update.AiWorkModel = work_model;
        update.AiContract = contract;
        update.AiExperienceYears = ExperienceYears;
        update.AiSkills = Skills;
        update.Fingerprint = Fingerprint;
        return true;
    }

    private static bool TryEnum<T>(string? value, out T parsed) where T : struct, Enum
    {
        parsed = default;
        return !string.IsNullOrEmpty(value) &&
               Enum.TryParse(value, ignoreCase: false, out parsed) &&
               Enum.IsDefined(parsed);
    }

    private static bool TryOptionalEnum<T>(string? value, out T? parsed) where T : struct, Enum
    {
        parsed = null;
        if (string.IsNullOrEmpty(value)) return true;
        if (!Enum.TryParse<T>(value, ignoreCase: false, out var item) || !Enum.IsDefined(item))
            return false;
        parsed = item;
        return true;
    }
}
