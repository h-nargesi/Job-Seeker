using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AiWorker;

public sealed class VerdictPayload
{
    public const int ReasonMaxLength = 2000;
    public const int SkillsMaxCount = 20;

    [JsonPropertyName("jobId")]
    public long JobId { get; set; }

    [JsonPropertyName("relevance")]
    public int Relevance { get; set; }

    [JsonPropertyName("verdict")]
    public string Verdict { get; set; } = string.Empty;

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
    public string Fingerprint { get; set; } = string.Empty;

    [JsonPropertyName("delta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Delta { get; set; }

    public static VerdictPayload Error(long jobId, string fingerprint, string reason)
    {
        var text = $"worker: {reason}";
        if (text.Length > ReasonMaxLength) text = text[..ReasonMaxLength];
        return new VerdictPayload
        {
            JobId = jobId,
            Relevance = 0,
            Verdict = "Error",
            Reason = text,
            Fingerprint = fingerprint,
        };
    }
}
