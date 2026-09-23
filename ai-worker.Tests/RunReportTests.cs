using System.Text.Json;

namespace AiWorker.Tests;

public sealed class RunReportTests
{
    [Fact]
    public void ShortHash_is_stable_and_case_insensitive_hex()
    {
        var rubric = "Judge this posting. Keywords: {{keywords}}.";

        var first = RunReport.ShortHash(rubric);
        var second = RunReport.ShortHash(rubric);

        Assert.Equal(first, second);
        Assert.Equal(RunReport.HashLength, first.Length);
        Assert.Matches("^[0-9a-f]+$", first);
        Assert.NotEqual(first, RunReport.ShortHash("different rubric"));
    }

    [Fact]
    public void From_maps_stats_and_options_into_the_payload()
    {
        var stats = new RunStats();
        var report = RunReport.From("20260923-101010", WorkerLoop.ExitOk, new LlmOptions
        {
            Model = "test-model",
            Rubric = "rubric",
            RubricTailor = "rubric-tailor",
            Temperature = 0.2,
            Seed = 42,
        }, stats);

        Assert.Equal("20260923-101010", report.RunId);
        Assert.Equal(WorkerLoop.ExitOk, report.ExitCode);
        Assert.Equal("test-model", report.Model);
        Assert.Equal(0.2, report.Temperature);
        Assert.Equal(42, report.Seed);
        Assert.Equal(RunReport.ShortHash("rubric"), report.RubricHash);
        Assert.Equal(RunReport.ShortHash("rubric-tailor"), report.RubricTailorHash);
        Assert.Empty(report.ErrorJobIds);
        Assert.True(DateTime.Parse(report.StartedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind) <=
                    DateTime.Parse(report.FinishedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind));
    }

    [Fact]
    public void From_without_options_defaults_temperature_and_hashes_to_null()
    {
        var report = RunReport.From("r", WorkerLoop.ExitUnexpected, null, new RunStats());

        Assert.Null(report.Model);
        Assert.Null(report.RubricHash);
        Assert.Null(report.RubricTailorHash);
        Assert.Equal(LlmOptions.DefaultTemperature, report.Temperature);
        Assert.Equal(0, report.Seed);
    }

    [Fact]
    public void ErrorJobIds_are_capped_at_fifty()
    {
        var stats = new RunStats();
        for (var i = 1; i <= 60; i++) stats.RecordErrorJob(i);

        var report = RunReport.From("r", 0, null, stats);

        Assert.Equal(RunStats.ErrorJobIdsCap, report.ErrorJobIds.Count);
        Assert.Equal(1, report.ErrorJobIds[0]);
        Assert.Equal(RunStats.ErrorJobIdsCap, report.ErrorJobIds[^1]);
    }

    [Fact]
    public void ToJson_uses_camel_case_and_carries_the_tier1_fields()
    {
        var stats = new RunStats();
        stats.Jobs = 3;
        stats.Promoted = 1;
        stats.Gone = 2;
        stats.RecordErrorJob(7);

        var json = JsonDocument.Parse(RunReport.From("r1", WorkerLoop.ExitOk, null, stats).ToJson()).RootElement;

        Assert.Equal("r1", json.GetProperty("runId").GetString());
        Assert.Equal(0, json.GetProperty("exitCode").GetInt32());
        Assert.Equal(3, json.GetProperty("jobs").GetInt32());
        Assert.Equal(1, json.GetProperty("promoted").GetInt32());
        Assert.Equal(2, json.GetProperty("gone404").GetInt32());
        Assert.Equal(7, json.GetProperty("errorJobIds")[0].GetInt64());
        Assert.True(json.TryGetProperty("startedUtc", out _));
        Assert.True(json.TryGetProperty("finishedUtc", out _));
        Assert.True(json.TryGetProperty("wallSeconds", out _));
    }
}
