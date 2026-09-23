using Newtonsoft.Json;

namespace Photon.JobSeeker;

public sealed class AiNextPayload
{
    public bool Empty { get; init; }

    public long? JobId { get; init; }

    public string? Content { get; init; }

    public string? Fingerprint { get; init; }

    public string? Options { get; init; }

    public string? ContextVersion { get; init; }

    public static AiNextPayload None { get; } = new() { Empty = true };

    public static AiNextPayload From(Job job, string? contextVersion)
    {
        return new AiNextPayload
        {
            Empty = false,
            JobId = job.JobID,
            Content = job.Content,
            Fingerprint = JobContent.Fingerprint(job.Content),
            Options = job.Options == null ? null : JsonConvert.SerializeObject(job.Options),
            ContextVersion = contextVersion,
        };
    }
}
