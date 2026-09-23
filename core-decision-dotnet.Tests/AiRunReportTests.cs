using Microsoft.AspNetCore.Mvc;

namespace Photon.JobSeeker.Tests;

public class AiRunReportTests
{
    private static AiController Controller(GoldenDatabase db)
    {
        return new AiController(db.Database, null!, null!, null!);
    }

    private static AiRunReportRequest Valid()
    {
        return new AiRunReportRequest
        {
            RunId = "20260923-101010",
            StartedUtc = "2026-09-23T10:10:10.0000000Z",
            FinishedUtc = "2026-09-23T10:12:10.0000000Z",
            ExitCode = 0,
            Model = "test-model",
            Temperature = 0.2,
            Seed = 42,
            RubricHash = "abc1234567",
            RubricTailorHash = "def9876543",
            Jobs = 3,
            Promoted = 1,
            ErrorVerdicts = 1,
            Gone404 = 0,
            Retries = 2,
            LlmFailures = 0,
            PromptTokens = 4500,
            CompletionTokens = 570,
            CallMs = 90000,
            WallSeconds = 120.5,
            FinishReasonLength = 1,
            TruncatedJobs = 1,
            DroppedMemoryRows = 2,
            ErrorJobIds = [7, 8],
        };
    }

    [Fact]
    public void Valid_report_round_trips()
    {
        using var db = new GoldenDatabase();

        Assert.IsType<OkResult>(Controller(db).RunReport(Valid()));

        var run = Assert.Single(db.Database.AiRun.Recent());
        Assert.Equal("20260923-101010", run.RunID);
        Assert.Equal("test-model", run.Model);
        Assert.Equal(0.2, run.Temperature);
        Assert.Equal(42, run.Seed);
        Assert.Equal("abc1234567", run.RubricHash);
        Assert.Equal(3, run.Jobs);
        Assert.Equal(1, run.Promoted);
        Assert.Equal(1, run.ErrorVerdicts);
        Assert.Equal(2, run.Retries);
        Assert.Equal(4500, run.PromptTokens);
        Assert.Equal(570, run.CompletionTokens);
        Assert.Equal(120.5, run.WallSeconds);
        Assert.Equal(1, run.FinishReasonLength);
        Assert.Equal(1, run.TruncatedJobs);
        Assert.Equal(2, run.DroppedMemoryRows);
        Assert.Equal([7, 8], run.ErrorJobIdList);
    }

    [Fact]
    public void Re_post_with_the_same_run_id_upserts_instead_of_duplicating()
    {
        using var db = new GoldenDatabase();
        var controller = Controller(db);

        Assert.IsType<OkResult>(controller.RunReport(Valid()));

        var repost = Valid();
        repost.Jobs = 5;
        repost.Promoted = 2;
        Assert.IsType<OkResult>(controller.RunReport(repost));

        var run = Assert.Single(db.Database.AiRun.Recent());
        Assert.Equal(5, run.Jobs);
        Assert.Equal(2, run.Promoted);
    }

    [Fact]
    public void Missing_body_is_rejected()
    {
        using var db = new GoldenDatabase();

        var result = Assert.IsType<BadRequestObjectResult>(Controller(db).RunReport(null));
        Assert.Equal(400, result.StatusCode);
    }

    [Theory]
    [InlineData(nameof(AiRunReportRequest.RunId), null!)]
    [InlineData(nameof(AiRunReportRequest.StartedUtc), "")]
    [InlineData(nameof(AiRunReportRequest.FinishedUtc), "")]
    [InlineData(nameof(AiRunReportRequest.ExitCode), 300)]
    [InlineData(nameof(AiRunReportRequest.Temperature), 3.0)]
    [InlineData(nameof(AiRunReportRequest.Jobs), -1)]
    [InlineData(nameof(AiRunReportRequest.Retries), -1)]
    [InlineData(nameof(AiRunReportRequest.PromptTokens), -5L)]
    [InlineData(nameof(AiRunReportRequest.WallSeconds), -0.1)]
    public void Invalid_fields_are_rejected(string field, object value)
    {
        using var db = new GoldenDatabase();
        var body = Valid();
        typeof(AiRunReportRequest).GetProperty(field)!.SetValue(body, value);

        var result = Assert.IsType<BadRequestObjectResult>(Controller(db).RunReport(body));
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void Too_many_error_job_ids_are_rejected()
    {
        using var db = new GoldenDatabase();
        var body = Valid();
        body.ErrorJobIds = Enumerable.Range(0, AiRunReportRequest.ErrorJobIdsMaxCount + 1)
            .Select(i => (long)i)
            .ToList();

        Assert.IsType<BadRequestObjectResult>(Controller(db).RunReport(body));
    }
}
