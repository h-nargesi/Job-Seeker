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

public sealed class AiMemoryPayload
{
    public IReadOnlyList<AiMemoryItem> Ranking { get; init; } = [];

    public IReadOnlyList<AiMemoryItem> Resume { get; init; } = [];

    public static AiMemoryPayload From(IReadOnlyList<MemoryRow> ranking, IReadOnlyList<MemoryRow> resume)
    {
        return new AiMemoryPayload
        {
            Ranking = ranking.Select(AiMemoryItem.From).ToList(),
            Resume = resume.Select(AiMemoryItem.From).ToList(),
        };
    }
}
