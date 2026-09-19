namespace Photon.JobSeeker;

public sealed class ResumePage
{
    public ResumeContext Context { get; init; } = new();

    public Dictionary<string, string> Text { get; init; } = [];
}
