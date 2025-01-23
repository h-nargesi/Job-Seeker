using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker;

[Route("[controller]/[action]")]
public class DecisionController(Analyzer analyzer) : Controller
{
    private readonly Analyzer analyzer = analyzer;

    [HttpPost]
    public IActionResult Take([FromBody] PageContext? context)
    {
        if (context == null) return BadRequest();

        try
        {
            Log.Debug("Taken: {0}", context.ToString());

            var result = analyzer.Analyze(context);

            return Ok(new
            {
                trend = result.TrendID,
                commands = result.Commands,
            });
        }
        catch (BadJobRequest bd)
        {
            Log.Error(bd.Message);
            return BadRequest();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Reset()
    {
        try
        {
            using var database = Database.Open();
            database.Trend.DeleteExpired(0);
            analyzer.ClearAgencies();

            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpGet]
    public IActionResult Scopes()
    {
        try
        {
            var agencies = analyzer.Agencies.Values.Select(a => new
            {
                a.Name,
                a.Domain,
                a.Waiting,
            });

            return Ok(agencies);
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpGet]
    public IActionResult Orders()
    {
        try
        {
            using var trends_checkpoint = new TrendsCheckpoint(analyzer);
            var result = trends_checkpoint.CheckCurrentTrends();
            return Ok(result);
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Running([FromBody] RunningMethodContext context)
    {
        try
        {
            if (context.Agency == null) return BadRequest();
            var agency = analyzer.Agencies[context.Agency];

            using var database = Database.Open();

            if (context.Running.HasValue)
            {
                agency.CurrentMethodIndex = context.Running.Value;
                agency.Status |= AgencyStatus.ActiveSeeking;
                database.Trend.ClearSearching(agency.ID);
            }
            else
            {
                agency.Status &= ~AgencyStatus.ActiveSeeking;
            }

            database.Agency.SaveState(agency);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }
}
