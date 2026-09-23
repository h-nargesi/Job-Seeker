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

    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; set; }

    [JsonPropertyName("options")]
    public string? Options { get; set; }

    [JsonPropertyName("contextVersion")]
    public string? ContextVersion { get; set; }
}

public sealed class InventoryItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("keys")]
    public List<string>? Keys { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
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
