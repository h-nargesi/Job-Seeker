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
            return Ok(new StatsDailyResponse(DateTime.Now, StatsDailyDays, null, null, null));
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
            return Ok(new StatsFullResponse(DateTime.Now, ParseFilters(agencies, countries)));
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
