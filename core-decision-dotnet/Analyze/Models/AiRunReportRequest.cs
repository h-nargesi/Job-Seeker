using System.Text.Json.Serialization;

namespace Photon.JobSeeker;

public sealed class AiRunReportRequest
{
    public const int RunIdMaxLength = 64;
    public const int TimestampMaxLength = 64;
    public const int ModelMaxLength = 200;
    public const int HashMaxLength = 32;
    public const int ErrorJobIdsMaxCount = 50;

    [JsonPropertyName("runId")]
    public string? RunId { get; set; }

    [JsonPropertyName("startedUtc")]
    public string? StartedUtc { get; set; }

    [JsonPropertyName("finishedUtc")]
    public string? FinishedUtc { get; set; }

    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    [JsonPropertyName("rubricHash")]
    public string? RubricHash { get; set; }

    [JsonPropertyName("rubricTailorHash")]
    public string? RubricTailorHash { get; set; }

    [JsonPropertyName("jobs")]
    public int? Jobs { get; set; }

    [JsonPropertyName("promoted")]
    public int? Promoted { get; set; }

    [JsonPropertyName("errorVerdicts")]
    public int? ErrorVerdicts { get; set; }

    [JsonPropertyName("gone404")]
    public int? Gone404 { get; set; }

    [JsonPropertyName("retries")]
    public int? Retries { get; set; }

    [JsonPropertyName("llmFailures")]
    public int? LlmFailures { get; set; }

    [JsonPropertyName("promptTokens")]
    public long? PromptTokens { get; set; }

    [JsonPropertyName("completionTokens")]
    public long? CompletionTokens { get; set; }

    [JsonPropertyName("callMs")]
    public long? CallMs { get; set; }

    [JsonPropertyName("wallSeconds")]
    public double? WallSeconds { get; set; }

    [JsonPropertyName("finishReasonLength")]
    public int? FinishReasonLength { get; set; }

    [JsonPropertyName("truncatedJobs")]
    public int? TruncatedJobs { get; set; }

    [JsonPropertyName("droppedMemoryRows")]
    public int? DroppedMemoryRows { get; set; }

    [JsonPropertyName("errorJobIds")]
    public List<long>? ErrorJobIds { get; set; }

    public bool TryCreate(out AiRun run, out string error)
    {
        run = new AiRun();
        error = string.Empty;

        if (string.IsNullOrEmpty(RunId) || RunId.Length > RunIdMaxLength)
        {
            error = $"runId is required (<= {RunIdMaxLength} characters)";
            return false;
        }

        if (string.IsNullOrEmpty(StartedUtc) || StartedUtc.Length > TimestampMaxLength ||
            string.IsNullOrEmpty(FinishedUtc) || FinishedUtc.Length > TimestampMaxLength)
        {
            error = "startedUtc and finishedUtc are required (<= 64 characters)";
            return false;
        }

        if (ExitCode is not int exit || exit < 0 || exit > 255)
        {
            error = "exitCode must be an integer 0-255";
            return false;
        }

        if (Model is { Length: > ModelMaxLength })
        {
            error = $"model must be <= {ModelMaxLength} characters";
            return false;
        }

        if (Temperature is not null and (< 0 or > 2))
        {
            error = "temperature must be within 0-2";
            return false;
        }

        if (RubricHash is { Length: > HashMaxLength } || RubricTailorHash is { Length: > HashMaxLength })
        {
            error = $"rubric hashes must be <= {HashMaxLength} characters";
            return false;
        }

        if (Jobs < 0 || Promoted < 0 || ErrorVerdicts < 0 || Gone404 < 0 || Retries < 0 || LlmFailures < 0 ||
            FinishReasonLength < 0 || TruncatedJobs < 0 || DroppedMemoryRows < 0 ||
            PromptTokens < 0 || CompletionTokens < 0 || CallMs < 0)
        {
            error = "counters must be >= 0";
            return false;
        }

        if (WallSeconds is < 0)
        {
            error = "wallSeconds must be >= 0";
            return false;
        }

        if (ErrorJobIds != null && ErrorJobIds.Count > ErrorJobIdsMaxCount)
        {
            error = $"errorJobIds must have <= {ErrorJobIdsMaxCount} items";
            return false;
        }

        run.RunID = RunId;
        run.StartedUtc = StartedUtc;
        run.FinishedUtc = FinishedUtc;
        run.ExitCode = ExitCode!.Value;
        run.Model = Model;
        run.Temperature = Temperature ?? 0;
        run.Seed = Seed ?? 0;
        run.RubricHash = RubricHash;
        run.RubricTailorHash = RubricTailorHash;
        run.Jobs = Jobs ?? 0;
        run.Promoted = Promoted ?? 0;
        run.ErrorVerdicts = ErrorVerdicts ?? 0;
        run.Gone404 = Gone404 ?? 0;
        run.Retries = Retries ?? 0;
        run.LlmFailures = LlmFailures ?? 0;
        run.PromptTokens = PromptTokens ?? 0;
        run.CompletionTokens = CompletionTokens ?? 0;
        run.CallMs = CallMs ?? 0;
        run.WallSeconds = WallSeconds ?? 0;
        run.FinishReasonLength = FinishReasonLength ?? 0;
        run.TruncatedJobs = TruncatedJobs ?? 0;
        run.DroppedMemoryRows = DroppedMemoryRows ?? 0;
        run.ErrorJobIds = ErrorJobIds == null || ErrorJobIds.Count == 0
            ? null
            : System.Text.Json.JsonSerializer.Serialize(ErrorJobIds);
        return true;
    }
}
