namespace Photon.JobSeeker;

public sealed class AssistantJobItem
{
    public long JobId { get; init; }

    public string? Title { get; init; }

    public string? Url { get; init; }

    public int? AiScore { get; init; }

    public bool PendingProposal { get; init; }

    public string ResumeText { get; init; } = string.Empty;
}
