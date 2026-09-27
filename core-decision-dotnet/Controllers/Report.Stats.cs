using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker;

public partial class ReportController
{
    private const int StatsDailyDays = 30;

    [HttpGet]
    public IActionResult Stats(string? agencies, string? countries)
    {
        try
        {
            return View("~/views/stats.cshtml", new StatsPageViewModel(agencies, countries));
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpGet]
    public IActionResult StatsDaily()
    {
        try
        {
            var daily = database.Job.StatsDailyStacked(StatsDailyDays);
            var velocity = database.Job.StatsVelocity(StatsDailyDays);
            var kpis = database.Job.StatsKpis();

            return Ok(new StatsDailyResponse(DateTime.Now, StatsDailyDays, daily, velocity, kpis));
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpGet]
    public IActionResult StatsFull(string? agencies, string? countries)
    {
        try
        {
            var filters = ParseFilters(agencies, countries);
            var yield = database.Job.StatsAgencyYield(filters.Agencies);

            return Ok(new StatsFullResponse(
                DateTime.Now,
                filters,
                yield,
                database.Job.StatsFunnel(yield),
                database.Job.StatsPipelineHealth(StatsDailyDays, filters.Agencies, filters.Countries),
                database.Job.StatsSkillsGap(filters.Agencies, filters.Countries),
                database.Job.StatsScoreHistograms(filters.Agencies, filters.Countries),
                database.Job.StatsAiDonuts(filters.Agencies, filters.Countries),
                database.Job.StatsAiVerdict(filters.Agencies, filters.Countries),
                database.Job.StatsCompetitiveness(filters.Agencies, filters.Countries),
                database.Job.StatsAttentionAging(filters.Agencies, filters.Countries),
                database.Job.StatsDispositionTimes(filters.Agencies, filters.Countries)));
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    private static StatsFilters ParseFilters(string? agencies, string? countries) => new(
        agencies?.Split(',').Select(id => id.Trim()).Where(id => !string.IsNullOrEmpty(id)).ToArray() ?? [],
        countries?.Split(',').Select(id => id.Trim()).Where(id => !string.IsNullOrEmpty(id)).ToArray() ?? []);
}
