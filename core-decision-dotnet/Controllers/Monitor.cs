using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker;

[Route("[controller]/[action]")]
public class MonitorController(Database database) : Controller
{
    private readonly Database database = database;

    [HttpGet("/monitor")]
    [HttpGet]
    public IActionResult Index()
    {
        try
        {
            return View("~/views/ai-monitor.cshtml", BuildModel());
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpGet]
    public IActionResult Body()
    {
        try
        {
            return View("~/views/ai-monitor-body.cshtml", BuildModel());
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    private MonitorViewModel BuildModel() => new(
        database.AiRun.Recent(30),
        database.Job.Calibration(),
        database.Job.QueueHealth());
}
