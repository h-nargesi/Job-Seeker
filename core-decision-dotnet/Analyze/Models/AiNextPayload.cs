using Newtonsoft.Json;

namespace Photon.JobSeeker;

public sealed class AiNextSettings
{
    public int Aipassmark { get; init; }
}

public sealed class AiNextPayload
{
    public bool Empty { get; init; }

    public long? JobId { get; init; }

    public string? Content { get; init; }

    public string? Resume { get; init; }

    public IReadOnlyList<JobKeyword>? Keywords { get; init; }

    public string? Fingerprint { get; init; }

    public AiNextSettings? Settings { get; init; }

    public string? Options { get; init; }

    public IReadOnlyList<ResumeInventoryItem>? Inventory { get; init; }

    public static AiNextPayload None { get; } = new() { Empty = true };

    public static AiNextPayload From(Job job, string resume, IReadOnlyList<JobKeyword> keywords, int aipassmark,
        ResumeInventory? inventory = null)
    {
        return new AiNextPayload
        {
            Empty = false,
            JobId = job.JobID,
            Content = job.Content,
            Resume = resume,
            Keywords = keywords,
            Fingerprint = JobContent.Fingerprint(job.Content),
            Settings = new AiNextSettings { Aipassmark = aipassmark },
            Options = job.Options == null ? null : JsonConvert.SerializeObject(job.Options),
            Inventory = inventory?.Items,
        };
    }
}
