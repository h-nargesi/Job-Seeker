using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker;

[Route("[controller]/[action]")]
public class AssistantController(Database database) : Controller
{
    private readonly Database database = database;

    [HttpGet]
    public async Task<IActionResult> Jobs()
    {
        try
        {
            var views = HttpContext.RequestServices.GetService<IViewRenderService>()
                ?? throw new Exception("The 'IViewRenderService' is not initialized.");

            var jobs = database.Job.FetchAttentionJobs();
            var items = new List<AssistantJobItem>(jobs.Count);

            foreach (var job in jobs)
            {
                var live = ResumeHtml.LiveText(job);

                var html = await views.RenderToStringAsync(HttpContext, "~/views/resume.cshtml",
                    new ResumePage { Context = ResumeHtml.Selection(job), Text = live });

                items.Add(new AssistantJobItem
                {
                    JobId = job.JobID,
                    Title = job.Title,
                    Url = job.Url,
                    AiScore = job.AiScore,
                    PendingProposal = ResumeHtml.HasPendingProposal(job),
                    ResumeText = ResumeHtml.RenderText(html, live),
                });
            }

            return Ok(items);
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult Applied([FromQuery] long jobid)
    {
        try
        {
            return database.Job.MarkApplied(jobid, JobBusiness.AppliedViaAssistant) ? Ok() : NotFound();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpGet]
    public IActionResult Memory([FromQuery] string? scope, [FromQuery] bool? confirmed)
    {
        try
        {
            var filter = ParseScope(scope, out var bad);
            if (bad) return BadRequest(new { error = "validation", message = "scope must be Resume/Apply/Ranking" });

            var items = database.Memory.List(filter, confirmed);
            return Ok(items);
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult MemorySave([FromBody] AssistantMemoryRequest? body)
    {
        try
        {
            if (body == null)
                return BadRequest(new { error = "validation", message = "missing body" });

            if (!body.TryCreate(out var row, out var error))
                return BadRequest(new { error = "validation", message = error });

            var id = database.Memory.Insert(row);
            return Ok(new { id });
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult MemoryEdit([FromQuery] long id, [FromBody] AssistantMemoryRequest? body)
    {
        try
        {
            var row = database.Memory.Fetch(id);
            if (row == null) return NotFound();

            if (body == null)
                return BadRequest(new { error = "validation", message = "missing body" });

            if (!body.TryPatch(row, out var error))
                return BadRequest(new { error = "validation", message = error });

            return database.Memory.Update(row) ? Ok() : NotFound();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult MemoryConfirm([FromQuery] long id, [FromQuery] bool confirmed = true)
    {
        try
        {
            return database.Memory.SetConfirmed(id, confirmed) ? Ok() : NotFound();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult MemoryDelete([FromQuery] long id)
    {
        try
        {
            return database.Memory.Delete(id) ? Ok() : NotFound();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public IActionResult MemoryBump([FromQuery] long id)
    {
        try
        {
            return database.Memory.BumpUseCount(id) ? Ok() : NotFound();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    private static MemoryScope? ParseScope(string? scope, out bool invalid)
    {
        invalid = false;
        if (string.IsNullOrEmpty(scope)) return null;

        if (Enum.TryParse(scope, ignoreCase: true, out MemoryScope parsed) && Enum.IsDefined(parsed))
            return parsed;

        invalid = true;
        return null;
    }
}
