using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker;

[Route("[controller]/[action]")]
public class DecisionController(Analyzer analyzer, Database database, TrendsCheckpoint trends_checkpoint) : Controller
{
    private const int CLOSE_TIMEOUT_MS = 90_000;
    private const int AUTH_CLOSE_TIMEOUT_MS = 600_000;

    private readonly Analyzer analyzer = analyzer;
    private readonly Database database = database;
    private readonly TrendsCheckpoint trends_checkpoint = trends_checkpoint;

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public IActionResult Take([FromBody] PageContext? context)
    {
        if (context == null) return BadRequest(new { error = "missing-context" });

        try
        {
            Log.Debug("Taken: {0}", context.ToString());

            var result = analyzer.Analyze(context);

            return Ok(new
            {
                trend = result.TrendID,
                commands = result.Commands,
                close_timeout_ms = result.State == TrendState.Auth ? AUTH_CLOSE_TIMEOUT_MS : CLOSE_TIMEOUT_MS,
            });
        }
        catch (BadJobRequest bd)
        {
            Log.Error(bd.Message);
            return BadRequest(new { error = "bad-job-request", message = bd.Message });
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Heartbeat([FromBody] HeartbeatContext? context)
    {
        try
        {
            if (context?.Trend != null && database.Trend.Touch(context.Trend.Value))
            {
                Log.Debug("Heartbeat touched trend {0}", context.Trend.Value);
                return Ok(new { trend = context.Trend });
            }

            Log.Debug("Heartbeat missed trend {0}", context?.Trend);

            return Ok(new { trend = (long?)null });
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
            database.Trend.DeleteExpired(0);
            database.Trend.DeleteExpiredReservations(0);
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
            if (!analyzer.Agencies.TryGetValue(context.Agency, out var agency)) return NotFound();

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
