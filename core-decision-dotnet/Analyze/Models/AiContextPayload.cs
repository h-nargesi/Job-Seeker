using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Photon.JobSeeker;

public sealed class AiMemoryItem
{
    public string Domain { get; init; } = "*";

    public string FieldKey { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string? Note { get; init; }

    public static AiMemoryItem From(MemoryRow row)
    {
        return new AiMemoryItem
        {
            Domain = row.AgencyDomain,
            FieldKey = row.FieldKey,
            Kind = row.Kind.ToString(),
            Value = row.Value,
            Note = row.Note,
        };
    }
}

public sealed record AiContextPayload
{
    public const int VersionLength = 12;

    private static readonly JsonSerializerOptions VersionJson = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<AiMemoryItem> RankingMemory { get; init; } = [];

    public IReadOnlyList<AiMemoryItem> ResumeMemory { get; init; } = [];

    public IReadOnlyList<JobKeyword> Keywords { get; init; } = [];

    public string? Resume { get; init; }

    public IReadOnlyList<ResumeInventoryItem>? Inventory { get; init; }

    public int AiPassmark { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContextVersion { get; init; }

    public static AiContextPayload From(
        IReadOnlyList<MemoryRow> ranking,
        IReadOnlyList<MemoryRow> resume,
        IReadOnlyList<JobKeyword> keywords,
        string? masterResume,
        ResumeInventory? inventory,
        int aiPassmark)
    {
        var payload = new AiContextPayload
        {
            RankingMemory = ranking.Select(AiMemoryItem.From).ToList(),
            ResumeMemory = resume.Select(AiMemoryItem.From).ToList(),
            Keywords = keywords,
            Resume = masterResume,
            Inventory = inventory?.Items,
            AiPassmark = aiPassmark,
        };
        return payload with { ContextVersion = VersionOf(payload) };
    }

    public static string VersionOf(AiContextPayload payload)
    {
        var bare = payload with { ContextVersion = null };
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(bare, VersionJson));
        return Convert.ToHexString(hash[..(VersionLength / 2)]).ToLowerInvariant();
    }
}
