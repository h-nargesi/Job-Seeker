using Microsoft.AspNetCore.Mvc;

namespace Photon.JobSeeker.Tests;

public class ReportStatsTests
{
    [Fact]
    public void StatsDaily_serves_envelope_with_null_sections()
    {
        using var cp = new CheckpointDatabase();
        var before = DateTime.Now.AddMinutes(-1);

        var result = new ReportController(cp.Analyzer, cp.Database).StatsDaily();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<StatsDailyResponse>(ok.Value!);
        Assert.Equal(30, envelope.Days);
        Assert.Null(envelope.DailyStacked);
        Assert.Null(envelope.Velocity);
        Assert.Null(envelope.Kpis);
        Assert.InRange(envelope.GeneratedAt, before, DateTime.Now.AddMinutes(1));
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
    }

    [Fact]
    public void StatsFull_without_filters_returns_empty_arrays()
    {
        using var cp = new CheckpointDatabase();

        var result = new ReportController(cp.Analyzer, cp.Database).StatsFull(null, "");

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<StatsFullResponse>(ok.Value!);
        Assert.Empty(envelope.Filters.Agencies);
        Assert.Empty(envelope.Filters.Countries);
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
