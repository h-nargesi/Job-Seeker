using Microsoft.AspNetCore.Mvc;

namespace Photon.JobSeeker.Tests;

public class ReportStatsTests
{
    [Fact]
    public void StatsDaily_returns_zero_filled_sections_for_last_30_days()
    {
        using var cp = new CheckpointDatabase();
        var before = DateTime.Now.AddMinutes(-1);

        var result = new ReportController(cp.Analyzer, cp.Database).StatsDaily();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<StatsDailyResponse>(ok.Value!);
        Assert.Equal(30, envelope.Days);
        Assert.InRange(envelope.GeneratedAt, before, DateTime.Now.AddMinutes(1));

        Assert.NotNull(envelope.DailyStacked);
        Assert.NotNull(envelope.Velocity);
        Assert.NotNull(envelope.Kpis);

        Assert.Equal(30, envelope.DailyStacked.Count);
        Assert.Equal(30, envelope.Velocity.Count);
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd"), envelope.DailyStacked[^1].Day);
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd"), envelope.Velocity[^1].Day);
        Assert.All(envelope.DailyStacked, row =>
        {
            Assert.Equal(0, row.Saved);
            Assert.Equal(0, row.Revaluation);
            Assert.Equal(0, row.GateRejected);
            Assert.Equal(0, row.InAi);
            Assert.Equal(0, row.Attention);
            Assert.Equal(0, row.Applied);
            Assert.Equal(0, row.Rejected);
        });
        Assert.All(envelope.Velocity, row =>
        {
            Assert.Equal(0, row.Applied);
            Assert.Equal(0, row.Rejected);
        });

        Assert.Equal(0, envelope.Kpis.AttentionBacklog);
        Assert.Null(envelope.Kpis.AvgDispositionDays);
        Assert.Null(envelope.Kpis.AttentionAvgAgeDays);
    }

    [Fact]
    public void StatsFull_echoes_parsed_filters()
    {
        using var cp = new CheckpointDatabase();

        var result = new ReportController(cp.Analyzer, cp.Database).StatsFull("LinkedIn, ,Indeed", " NL ,");

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<StatsFullResponse>(ok.Value!);
        Assert.Equal(["LinkedIn", "Indeed"], envelope.Filters.Agencies);
        Assert.Equal(["NL"], envelope.Filters.Countries);
        Assert.NotNull(envelope.AgencyYield);
        Assert.NotNull(envelope.Funnel);
        Assert.NotNull(envelope.PipelineHealth);
        Assert.NotNull(envelope.SkillsGap);
    }

    [Fact]
    public void StatsFull_without_filters_returns_filled_sections()
    {
        using var cp = new CheckpointDatabase();

        var result = new ReportController(cp.Analyzer, cp.Database).StatsFull(null, "");

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<StatsFullResponse>(ok.Value!);
        Assert.Empty(envelope.Filters.Agencies);
        Assert.Empty(envelope.Filters.Countries);

        var yieldRow = Assert.Single(envelope.AgencyYield!);
        Assert.Equal("CheckpointAgency", yieldRow.Title);
        Assert.Equal(0, yieldRow.JobCount);
        Assert.Equal(0, yieldRow.AnalyzingRate);
        Assert.Equal(0, yieldRow.AcceptingRate);

        Assert.Equal(0, envelope.Funnel!.Overall.Saved);
        Assert.Equal(0, envelope.Funnel.Overall.Analyzed);
        Assert.Equal(0, envelope.Funnel.Overall.Attention);
        Assert.Equal(0, envelope.Funnel.Overall.Applied);
        Assert.Empty(envelope.Funnel.Agencies);

        Assert.Equal(30, envelope.PipelineHealth!.Count);
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd"), envelope.PipelineHealth[^1].Day);
        Assert.All(envelope.PipelineHealth, row =>
        {
            Assert.Equal(0, row.AiPending);
            Assert.Equal(0, row.AiError);
        });

        Assert.NotNull(envelope.SkillsGap);
        Assert.Equal(0, envelope.SkillsGap!.Jobs);
        Assert.Empty(envelope.SkillsGap.Top);
    }

    [Fact]
    public void Stats_returns_view_with_prefilled_filters()
    {
        using var cp = new CheckpointDatabase();

        var result = new ReportController(cp.Analyzer, cp.Database).Stats("LinkedIn", "NL");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/views/stats.cshtml", view.ViewName);
        var model = Assert.IsType<StatsPageViewModel>(view.Model!);
        Assert.Equal("LinkedIn", model.Agencies);
        Assert.Equal("NL", model.Countries);
    }
}
