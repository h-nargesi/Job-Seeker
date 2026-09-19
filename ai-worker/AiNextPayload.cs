using System.Text.Json.Serialization;

namespace AiWorker;

public sealed class AiNextPayload
{
    [JsonPropertyName("empty")]
    public bool Empty { get; set; }

    [JsonPropertyName("jobId")]
    public long? JobId { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("resume")]
    public string? Resume { get; set; }

    [JsonPropertyName("keywords")]
    public List<JobKeyword>? Keywords { get; set; }

    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; set; }
}

public sealed class JobKeyword
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public long Score { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}
