using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AiWorker;

public sealed class RunReport
{
    public const int HashLength = 10;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string RunId { get; set; } = string.Empty;

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

    public List<long> ErrorJobIds { get; set; } = [];

    public static RunReport From(string runId, int exitCode, LlmOptions? options, RunStats stats)
    {
        return new RunReport
        {
            RunId = runId,
            StartedUtc = stats.StartedUtc.ToString("O"),
            FinishedUtc = DateTime.UtcNow.ToString("O"),
            ExitCode = exitCode,
            Model = options?.Model,
            Temperature = options?.Temperature ?? LlmOptions.DefaultTemperature,
            Seed = options?.Seed ?? 0,
            RubricHash = options == null ? null : ShortHash(options.Rubric),
            RubricTailorHash = options == null ? null : ShortHash(options.RubricTailor),
            Jobs = stats.Jobs,
            Promoted = stats.Promoted,
            ErrorVerdicts = stats.ErrorVerdicts,
            Gone404 = stats.Gone,
            Retries = stats.Retries,
            LlmFailures = stats.LlmFailures,
            PromptTokens = stats.PromptTokens,
            CompletionTokens = stats.CompletionTokens,
            CallMs = stats.CallMs,
            WallSeconds = stats.WallSeconds,
            FinishReasonLength = stats.FinishReasonLength,
            TruncatedJobs = stats.TruncatedJobs,
            DroppedMemoryRows = stats.DroppedMemoryRows,
            ErrorJobIds = [.. stats.ErrorJobIds],
        };
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, Json);
    }

    public static string ShortHash(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash[..(HashLength / 2)]).ToLowerInvariant();
    }
}
