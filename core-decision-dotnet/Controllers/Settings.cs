using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker;

[Route("[controller]/[action]")]
public class SettingsController(Analyzer analyzer, Database database) : Controller
{
    private readonly Analyzer analyzer = analyzer;
    private readonly Database database = database;

    [HttpGet("/settings")]
    [HttpGet]
    public IActionResult Index()
    {
        try
        {
            var model = new SettingsPageModel(
                AppSettingBusiness.Fields,
                database.AppSetting.FetchAll(),
                database.JobOption.FetchRows(),
                analyzer.AgenciesByID.Values
                    .OrderBy(a => a.Name)
                    .Select(a => new AgencyPacingItem(a.Name, a.PacingOverride, a.DefaultPacing))
                    .ToList());

            return View("~/views/settings.cshtml", model);
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult AppSetting([FromBody] AppSettingContext? context)
    {
        try
        {
            if (context?.Key == null) return BadRequest();

            database.AppSetting.Save(context.Key, context.Value);
            return Ok();
        }
        catch (BadJobRequest ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult OptionSave([FromBody] OptionEditRow? row)
    {
        try
        {
            if (row == null) return BadRequest();

            database.JobOption.Save(row);
            JobEligibilityHelper.InvalidateOptionsCache();
            return Ok();
        }
        catch (BadJobRequest ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult OptionDelete([FromQuery] long joboptionid)
    {
        try
        {
            var done = database.JobOption.Delete(joboptionid);
            if (done) JobEligibilityHelper.InvalidateOptionsCache();
            return done ? Ok() : NotFound();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult OptionToggle([FromQuery] long joboptionid, [FromQuery] bool effective)
    {
        try
        {
            var done = database.JobOption.SetEffective(joboptionid, effective);
            if (done) JobEligibilityHelper.InvalidateOptionsCache();
            return done ? Ok() : NotFound();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Reload()
    {
        try
        {
            analyzer.ReloadSettings();
            JobEligibilityHelper.InvalidateOptionsCache();
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult AgencyWaiting([FromBody] AgencyWaitingContext? context)
    {
        try
        {
            if (context?.Agency == null) return BadRequest();

            var agency = analyzer.FindAgency(context.Agency);
            if (agency == null) return NotFound();

            if (context.Waiting is < 0 or > 600_000)
                return BadRequest(new { error = "waiting-out-of-range" });

            agency.ApplyWaiting(context.Waiting, database);
            analyzer.ReloadSettings();

            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    public class AppSettingContext
    {
        public string? Key { get; set; }

        public string? Value { get; set; }
    }
}
