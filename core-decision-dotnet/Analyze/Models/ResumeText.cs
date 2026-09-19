using Newtonsoft.Json;

namespace Photon.JobSeeker;

public sealed class ResumeText : Dictionary<string, ResumeTextSlot>
{
}

public sealed class ResumeTextSlot
{
    [JsonProperty("live")]
    public string? Live { get; set; }

    [JsonProperty("proposal")]
    public string? Proposal { get; set; }

    [JsonProperty("status")]
    public ResumeTextSlotStatus Status { get; set; }
}

public enum ResumeTextSlotStatus
{
    Pending,
    Accepted,
    Rejected,
}
