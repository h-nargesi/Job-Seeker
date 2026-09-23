using Serilog;

namespace AiWorker;

public static class WorkerLog
{
    public const int KeepRuns = 30;

    public static string RunId { get; private set; } = string.Empty;

    public static string RunsDirectory { get; private set; } = "logs/runs";

    public static void Debug(string template, params object[] args)
    {
        Log.Debug(template, args);
    }

    public static void Info(string template, params object[] args)
    {
        Log.Information(template, args);
    }

    public static void Warn(string template, params object[] args)
    {
        Log.Warning(template, args);
    }

    public static void Error(string template, params object[] args)
    {
        Log.Error(template, args);
    }

    public static void Error(Exception exception, string template, params object[] args)
    {
        Log.Error(exception, template, args);
    }

    public static string Snippet(string text, int max = 400)
    {
        var flattened = text.ReplaceLineEndings(" ");
        return flattened.Length <= max ? flattened : $"{flattened[..max]}...";
    }

    public static void StartRun(string runsDirectory, int keep = KeepRuns, DateTime? now = null)
    {
        RunsDirectory = runsDirectory;
        Directory.CreateDirectory(runsDirectory);
        RunId = NewRunId(runsDirectory, now);
        Prune(runsDirectory, keep);
    }

    public static string NewRunId(string runsDirectory, DateTime? now = null)
    {
        var id = (now ?? DateTime.Now).ToString("yyyyMMdd-HHmmss");
        if (Directory.EnumerateFiles(runsDirectory, id + ".*").Any())
            id += "-" + Environment.ProcessId;
        return id;
    }

    public static void Prune(string directory, int keep)
    {
        try
        {
            var stale = Directory.EnumerateFiles(directory)
                .Select(Path.GetFileNameWithoutExtension)
                .Distinct()
                .OrderByDescending(id => id, StringComparer.Ordinal)
                .Skip(keep);

            foreach (var id in stale)
                foreach (var path in Directory.EnumerateFiles(directory, id + ".*"))
                    File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warning("run-log retention prune failed: {Message}", ex.Message);
        }
    }

    public static void WriteRunSummary(string json)
    {
        try
        {
            Directory.CreateDirectory(RunsDirectory);
            File.WriteAllText(Path.Combine(RunsDirectory, RunId + ".json"), json);
        }
        catch (Exception ex)
        {
            Log.Warning("run summary for run {RunId} could not be written: {Message}", RunId, ex.Message);
        }
    }

    public static void WriteFailureDump(long jobId, int callNo, int attempt, string rawOutput)
    {
        try
        {
            var directory = Path.Combine("logs/failures", RunId);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, $"job-{jobId}-call{callNo}-attempt{attempt}.txt"), rawOutput);
        }
        catch (Exception ex)
        {
            Log.Warning("failure dump for job {JobId} could not be written: {Message}", jobId, ex.Message);
        }
    }
}
