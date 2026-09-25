using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Text;

namespace Photon.JobSeeker;

[Route("[controller]/[action]")]
public class JobController(Analyzer analyzer, Database database, IDatabaseFactory database_factory) : Controller
{
    private readonly Analyzer analyzer = analyzer;
    private readonly Database database = database;
    private readonly IDatabaseFactory database_factory = database_factory;

    [HttpGet("{jobid:int}")]
    public IActionResult Get([FromRoute] long jobid)
    {
        try
        {
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
            return database.Job.MarkApplied(jobid, JobBusiness.AppliedViaDashboard) ? Ok() : NotFound();
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
            if (resume != null) resume.HumanEdited = true;
            database.Job.ChangeOptions(jobid, resume);
            return Ok(resume?.SimlpeSerialize());
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult AcceptAi([FromQuery] long jobid)
    {
        try
        {
            return database.Job.AcceptAiOptions(jobid) ? Ok() : BadRequest();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult ResumeText([FromQuery] long jobid, [FromQuery] string slot, [FromQuery] string op,
        [FromBody] string? text)
    {
        try
        {
            var done = op switch
            {
                "accept" => database.Job.AcceptTextSlot(jobid, slot),
                "reject" => database.Job.RejectTextSlot(jobid, slot),
                "live" => database.Job.WriteLiveText(jobid, slot, text),
                _ => false,
            };

            return done ? Ok() : BadRequest();
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

            var job = database.Job.Fetch(jobid);
            if (job == null) return NotFound();
            var page = new ResumePage { Context = ResumeHtml.Selection(job), Text = ResumeHtml.LiveText(job) };

            var result = await resume_generator.RenderToStringAsync(HttpContext, "~/views/resume.cshtml", page);
            var content = Encoding.UTF8.GetBytes(result);

            return File(content, "text/html", page.Context.FileName("html"));
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
            var job = database.Job.Fetch(jobid);
            if (job == null) return NotFound();

            var page = new ResumePage { Context = ResumeHtml.Selection(job), Text = ResumeHtml.LiveText(job) };
            return View("~/views/resume.cshtml", page);
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Revaluate([FromQuery] long? jobid)
    {
        try
        {
            if (jobid is long id)
                return ForceRevaluate(id);

            JobEligibilityHelper.RunRevaluateProcess(analyzer, database_factory);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Requeue([FromQuery] long jobid)
    {
        try
        {
            return database.Job.RequeueJob(jobid) ? Ok() : BadRequest();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Promote([FromQuery] long jobid)
    {
        try
        {
            return database.Job.PromoteJob(jobid) ? Ok() : BadRequest();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Clean([FromQuery] bool vacuum = false)
    {
        try
        {
            database.Job.Clean(3, vacuum);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    private IActionResult ForceRevaluate(long jobid)
    {
        var job = database.Job.Fetch(jobid);
        if (job == null) return NotFound();
        if (job.Content == null) return BadRequest();
        if (job.State is JobState.Rejected or JobState.Applied) return BadRequest();

        analyzer.AgenciesByID.TryGetValue(job.AgencyID, out var agency);
        using var evaluator = new JobEligibilityHelper(database_factory);
        evaluator.EvaluateJobEligibility(job, agency?.JobAcceptabilityChecker, force: true);
        return Ok();
    }
}