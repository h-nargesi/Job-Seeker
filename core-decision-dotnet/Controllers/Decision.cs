using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker;

[Route("[controller]/[action]")]
public class DecisionController(Analyzer analyzer, Database database, TrendsCheckpoint trends_checkpoint) : Controller
{
    private const int CLOSE_TIMEOUT_MS = 90_000;
    private const int AUTH_CLOSE_TIMEOUT_MS = 600_000;
    private const int CHALLENGE_CLOSE_TIMEOUT_MS = 86_400_000;

    private readonly Analyzer analyzer = analyzer;
    private readonly Database database = database;
    private readonly TrendsCheckpoint trends_checkpoint = trends_checkpoint;

    private static readonly Random pacing_random = new();

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public IActionResult Take([FromBody] PageContext? context)
    {
        if (context == null) return BadRequest(new { error = "missing-context" });

        try
        {
            Log.Debug("Taken: {0}", context.ToString());

            var result = analyzer.Analyze(context, database);

            InsertPacingWait(result, context.Agency);

            return Ok(new
            {
                trend = result.TrendID,
                commands = result.Commands,
                close_timeout_ms = context.Challenge ? CHALLENGE_CLOSE_TIMEOUT_MS
                    : result.State == TrendState.Auth ? AUTH_CLOSE_TIMEOUT_MS : CLOSE_TIMEOUT_MS,
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

    private void InsertPacingWait(Result result, string? agency_name)
    {
        if (agency_name == null || result.Commands.Length == 0) return;

        var pacing = analyzer.FindAgency(agency_name)?.Pacing ?? 0;
        if (pacing <= 0) return;

        var index = Array.FindIndex(result.Commands,
            c => c.page_action is PageAction.go or PageAction.open or PageAction.click);
        if (index < 0) return;

        var delta = (int)Math.Round(pacing * 0.25);
        var miliseconds = pacing - delta + pacing_random.Next(2 * delta + 1);

        Log.Debug("Pacing wait ({0}): {1} ms before command #{2}", agency_name, miliseconds, index);

        var paced = new Command[result.Commands.Length + 1];
        Array.Copy(result.Commands, paced, index);
        paced[index] = Command.Wait(miliseconds);
        Array.Copy(result.Commands, index, paced, index + 1, result.Commands.Length - index);
        result.Commands = paced;
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
                waiting = a.DefaultWaiting,
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
    public IActionResult Running([FromBody] RunningMethodContext? context)
    {
        try
        {
            if (context?.Agency == null || context.Running == null) return BadRequest();

            var agency = analyzer.FindAgency(context.Agency);
            if (agency == null) return NotFound();

            if (context.Running.Value < 0 || context.Running.Value >= agency.SearchingMethodCount)
                return BadRequest(new { error = "running-out-of-range" });

            agency.ApplyRunning(context.Running.Value, database);
            analyzer.ReloadSettings();

            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Status([FromBody] AgencyStatusContext? context)
    {
        try
        {
            if (context?.Agency == null) return BadRequest();

            var agency = analyzer.FindAgency(context.Agency);
            if (agency == null) return NotFound();

            if (context.Seeking == true && agency.SearchingMethodCount == 0)
                return BadRequest(new { error = "no-methods" });

            agency.ApplyStatus(context.Seeking, context.Analyzing, database);
            analyzer.ReloadSettings();

            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }
}
