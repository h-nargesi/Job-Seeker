namespace Photon.JobSeeker;

public class AiRun
{
    public string RunID { get; set; } = string.Empty;

    public string StartedUtc { get; set; } = string.Empty;

    public string FinishedUtc { get; set; } = string.Empty;

    public int ExitCode { get; set; }

    public string? Model { get; set; }

    public double Temperature { get; set; }

    public int Seed { get; set; }

    public string? RubricHash { get; set; }

    public string? RubricTailorHash { get; set; }

    public int Jobs { get; set; }

    public int Promoted { get; set; }

    public int ErrorVerdicts { get; set; }

    public int Gone404 { get; set; }

    public int Retries { get; set; }

    public int LlmFailures { get; set; }

    public long PromptTokens { get; set; }

    public long CompletionTokens { get; set; }

    public long CallMs { get; set; }

    public double WallSeconds { get; set; }

    public int FinishReasonLength { get; set; }

    public int TruncatedJobs { get; set; }

    public int DroppedMemoryRows { get; set; }

    public string? ErrorJobIds { get; set; }

    public List<long> ErrorJobIdList =>
        string.IsNullOrEmpty(ErrorJobIds) ? [] :
        System.Text.Json.JsonSerializer.Deserialize<List<long>>(ErrorJobIds) ?? [];
}
