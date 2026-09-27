using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker;

[Route("[controller]/[action]")]
public class MonitorController(Database database) : Controller
{
    private readonly Database database = database;

    [HttpGet("/monitor")]
    [HttpGet]
    public IActionResult Index(string? group)
    {
        try
        {
            return View("~/views/ai-monitor.cshtml", BuildModel(group));
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpGet]
    public IActionResult Body(string? group)
    {
        try
        {
            return View("~/views/ai-monitor-body.cshtml", BuildModel(group));
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    private MonitorViewModel BuildModel(string? group)
    {
        var stages_group = group == JobBusiness.GroupCountry ? JobBusiness.GroupCountry : JobBusiness.GroupAgency;
        return new MonitorViewModel(
            database.AiRun.Recent(30),
            database.Job.Calibration(),
            database.Job.QueueHealth(),
            database.Job.Stages(stages_group),
            stages_group,
            database.Job.VerdictDistribution(),
            database.Job.PendingAges());
    }
}
