using System.Text.Json.Serialization;

namespace AiWorker;

public sealed class AiContext
{
    [JsonPropertyName("rankingMemory")]
    public List<MemorySnapshotRow> RankingMemory { get; set; } = [];

    [JsonPropertyName("resumeMemory")]
    public List<MemorySnapshotRow> ResumeMemory { get; set; } = [];

    [JsonPropertyName("keywords")]
    public List<JobKeyword>? Keywords { get; set; }

    [JsonPropertyName("resume")]
    public string? Resume { get; set; }

    [JsonPropertyName("inventory")]
    public List<InventoryItem>? Inventory { get; set; }

    [JsonPropertyName("aiPassmark")]
    public int AiPassmark { get; set; }

    [JsonPropertyName("contextVersion")]
    public string? ContextVersion { get; set; }
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
