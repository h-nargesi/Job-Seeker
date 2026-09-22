using Serilog;

namespace AiWorker;

public static class WorkerLog
{
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

    public static void WriteFailureDump(long jobId, int callNo, int attempt, string rawOutput)
    {
        try
        {
            Directory.CreateDirectory("logs/failures");
            File.WriteAllText($"logs/failures/job-{jobId}-call{callNo}-attempt{attempt}.txt", rawOutput);
        }
        catch (Exception ex)
        {
            Log.Warning("failure dump for job {JobId} could not be written: {Message}", jobId, ex.Message);
        }
    }
}
