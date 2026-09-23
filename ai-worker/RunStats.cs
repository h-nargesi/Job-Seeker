using System.Diagnostics;

namespace AiWorker;

public sealed class RunStats
{
    public const int ErrorJobIdsCap = 50;

    public int Jobs;
    public int Promoted;
    public int ErrorVerdicts;
    public int Gone;
    public int Retries;
    public int LlmFailures;
    public int FinishReasonLength;
    public int DroppedMemoryRows;
    public long PromptTokens;
    public long CompletionTokens;
    public long CallMs;

    private readonly HashSet<long> truncatedJobs = [];
    private readonly List<long> errorJobIds = [];

    public DateTime StartedUtc { get; } = DateTime.UtcNow;

    public Stopwatch Watch { get; } = Stopwatch.StartNew();

    public double WallSeconds => Watch.Elapsed.TotalSeconds;

    public double AvgSecondsPerJob => Jobs == 0 ? 0 : WallSeconds / Jobs;

    public int TruncatedJobs => truncatedJobs.Count;

    public IReadOnlyList<long> ErrorJobIds => errorJobIds;

    public void RecordTruncationIfAny(long jobId, PromptBuilder.Prompt message)
    {
        if (message.ContentTruncatedChars > 0) truncatedJobs.Add(jobId);
        if (message.DroppedMemoryRows > 0) DroppedMemoryRows += message.DroppedMemoryRows;
    }

    public void RecordErrorJob(long jobId)
    {
        if (errorJobIds.Count < ErrorJobIdsCap) errorJobIds.Add(jobId);
    }

    public void Add(LlmResult result)
    {
        PromptTokens += result.PromptTokens ?? 0;
        CompletionTokens += result.CompletionTokens ?? 0;
        CallMs += result.ElapsedMs;
        if (result.FinishReason == "length") FinishReasonLength++;
    }
}
