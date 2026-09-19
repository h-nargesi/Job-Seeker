using System.Text.Json.Serialization;

namespace AiWorker;

public sealed class MemorySnapshot
{
    [JsonPropertyName("ranking")]
    public List<MemorySnapshotRow> Ranking { get; set; } = [];

    [JsonPropertyName("resume")]
    public List<MemorySnapshotRow> Resume { get; set; } = [];
}

public sealed class MemorySnapshotRow
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = "*";

    [JsonPropertyName("fieldKey")]
    public string FieldKey { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}
