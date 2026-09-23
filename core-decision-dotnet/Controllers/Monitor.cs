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
            var model = new MonitorViewModel(
                database.AiRun.Recent(30),
                database.Job.Calibration(),
                database.Job.QueueHealth());

            return View("~/views/ai-monitor.cshtml", model);
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }
}
