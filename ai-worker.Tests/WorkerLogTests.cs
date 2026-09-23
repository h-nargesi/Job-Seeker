namespace AiWorker.Tests;

[Collection("worker-run")]
public sealed class WorkerLogTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ai-worker-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private string Runs()
    {
        var directory = Path.Combine(root, "runs");
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Fact]
    public void NewRunId_appends_pid_on_same_second_collision()
    {
        var directory = Runs();
        var now = new DateTime(2026, 9, 23, 10, 11, 12);
        File.WriteAllText(Path.Combine(directory, now.ToString("yyyyMMdd-HHmmss") + ".log"), "old");

        var id = WorkerLog.NewRunId(directory, now);

        Assert.StartsWith(now.ToString("yyyyMMdd-HHmmss") + "-", id);
        Assert.EndsWith(Environment.ProcessId.ToString(), id);
    }

    [Fact]
    public void StartRun_sets_run_id_and_writes_summary_json()
    {
        var directory = Runs();
        WorkerLog.StartRun(directory, keep: 5, now: new DateTime(2026, 9, 23, 10, 11, 12));

        Assert.Equal(new DateTime(2026, 9, 23, 10, 11, 12).ToString("yyyyMMdd-HHmmss"), WorkerLog.RunId);
        Assert.Equal(directory, WorkerLog.RunsDirectory);

        WorkerLog.WriteRunSummary("{\"runId\":\"" + WorkerLog.RunId + "\"}");

        var path = Path.Combine(directory, WorkerLog.RunId + ".json");
        Assert.True(File.Exists(path));
        Assert.Contains("\"runId\"", File.ReadAllText(path));
    }

    [Fact]
    public void Prune_keeps_the_newest_thirty_runs_and_deletes_older_pairs()
    {
        var directory = Runs();
        for (var i = 1; i <= 32; i++)
        {
            var id = $"20260923-{i:D2}0000";
            File.WriteAllText(Path.Combine(directory, id + ".log"), "log");
            File.WriteAllText(Path.Combine(directory, id + ".json"), "json");
        }

        WorkerLog.Prune(directory, 30);

        var remaining = Directory.GetFiles(directory)
            .Select(Path.GetFileNameWithoutExtension)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        Assert.Equal(30, remaining.Count);
        Assert.DoesNotContain(remaining, id => id.EndsWith("010000") || id.EndsWith("020000"));
    }

    [Fact]
    public void WriteFailureDump_goes_under_the_current_run_directory()
    {
        var directory = Runs();
        WorkerLog.StartRun(directory, keep: 5, now: new DateTime(2026, 9, 23, 10, 11, 12));

        WorkerLog.WriteFailureDump(9, 1, 2, "raw output");

        var path = Path.Combine("logs/failures", WorkerLog.RunId, "job-9-call1-attempt2.txt");
        Assert.True(File.Exists(path));
        Assert.Equal("raw output", File.ReadAllText(path));
    }
}
