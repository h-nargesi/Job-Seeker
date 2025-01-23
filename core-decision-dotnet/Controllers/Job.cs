using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Text;

namespace Photon.JobSeeker;

[Route("[controller]/[action]")]
public class JobController(Analyzer analyzer) : Controller
{
    private readonly Analyzer analyzer = analyzer;

    [HttpGet("{jobid:int}")]
    public IActionResult Get([FromRoute] long jobid)
    {
        try
        {
            using var database = Database.Open();
            var job = database.Job.Fetch(jobid);
            if (job == null) return NotFound();
            analyzer.AgenciesByID.TryGetValue(job.AgencyID, out var agency);
            return View("~/views/job-detail.cshtml", (job, agency, string.Empty));
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Apply([FromQuery] long jobid)
    {
        try
        {
            using var database = Database.Open();
            database.Job.ChangeState(jobid, JobState.Applied);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Reject([FromQuery] long jobid)
    {
        try
        {
            using var database = Database.Open();
            database.Job.RemoveHtmlContent(jobid);
            database.Job.ChangeState(jobid, JobState.Rejected);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Options([FromQuery] long jobid, [FromBody] string options)
    {
        try
        {
            var resume = ResumeContext.SimlpeDeserialize(options);
            using var database = Database.Open();
            database.Job.ChangeOptions(jobid, resume);
            return Ok(resume?.SimlpeSerialize());
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpGet]
    public async Task<IActionResult> Resume64([FromQuery] long jobid)
    {
        try
        {
            var resume_generator = HttpContext.RequestServices.GetService<IViewRenderService>() ??
                throw new Exception("The 'IViewRenderService' is not initialized.");

            using var database = Database.Open();
            var context = database.Job.FetchOptions(jobid) ?? new ResumeContext();

            var result = await resume_generator.RenderToStringAsync(HttpContext, "~/views/resume.cshtml", context);
            var content = Encoding.UTF8.GetBytes(result);

            return File(content, "text/html", context.FileName("html"));
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpGet]
    public IActionResult Resume([FromQuery] long jobid)
    {
        try
        {
            using var database = Database.Open();
            var context = database.Job.FetchOptions(jobid) ?? new ResumeContext();

            return View("~/views/resume.cshtml", context);
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Revaluate()
    {
        try
        {
            JobEligibilityHelper.RunRevaluateProcess(analyzer);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Clean()
    {
        try
        {
            using var database = Database.Open();
            database.Job.Clean(3);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpGet]
    public IActionResult Options()
    {
        try
        {
            return View("~/views/job-options.cshtml");
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Setting([FromBody] SettingContext options)
    {
        try
        {
            if (options.Query == null)
                return Ok("No Result");

            if (options.Query == "reload")
            {
                analyzer.ReloadSettings();
                return Ok("Setting were reloaded");
            }
            else
            {
                using var database = Database.Open();
                if (options.Type == "E")
                {
                    database.Execute(options.Query);
                    return Ok("Done");
                }
                else if (options.Type == "Q")
                {
                    var result = database.ReadAll(options.Query);
                    return Ok(result);
                }
            }

            return Ok("No Result");
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    public class SettingContext
    {
        public string? Type { get; set; }

        public string? Query { get; set; }
    }
}