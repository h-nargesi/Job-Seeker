using System.Diagnostics;

namespace AiWorker;

internal sealed class RunStats
{
    public int Jobs;
    public int Promoted;
    public int ErrorVerdicts;
    public int Gone;
    public int Retries;
    public long PromptTokens;
    public long CompletionTokens;
    public long CallMs;

    public Stopwatch Watch { get; } = Stopwatch.StartNew();

    public double WallSeconds => Watch.Elapsed.TotalSeconds;

    public double AvgSecondsPerJob => Jobs == 0 ? 0 : WallSeconds / Jobs;

    public void Add(LlmResult result)
    {
        PromptTokens += result.PromptTokens ?? 0;
        CompletionTokens += result.CompletionTokens ?? 0;
        CallMs += result.ElapsedMs;
    }
}
