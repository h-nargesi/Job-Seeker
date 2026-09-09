using Newtonsoft.Json;

namespace Photon.JobSeeker;

public sealed class JobOptionSettings
{
    [JsonProperty("resume")]
    public ResumeSettings? Resume { get; set; }

    [JsonProperty("money")]
    public int? Money { get; set; }

    [JsonProperty("period")]
    public int? Period { get; set; }
}

public sealed class ResumeSettings
{
    [JsonProperty("key")]
    public string? Key { get; set; }

    [JsonProperty("include_matched")]
    public bool? IncludeMatched { get; set; }

    [JsonProperty("parent")]
    public string? Parent { get; set; }
}
