using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker;

[Route("[controller]/[action]")]
public class AiController(
    Database database,
    IDatabaseFactory database_factory,
    MasterResumeCache master_resume,
    ResumeInventoryCache inventory_cache) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Next()
    {
        try
        {
            var job = database.Job.FetchNextAiPending();
            if (job == null) return Ok(AiNextPayload.None);

            var resume = await master_resume.GetAsync(HttpContext);
            var inventory = await inventory_cache.GetAsync(HttpContext);
            var keywords = JobKeywords.From(JobEligibilityHelper.SharedOptions(database_factory));
            return Ok(AiNextPayload.From(job, resume, keywords, database.AppSetting.AiPassmark(), inventory));
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpGet]
    public IActionResult Memory()
    {
        try
        {
            var cap = database.AppSetting.MemoryCap();
            var ranking = database.Memory.Snapshot(MemoryScope.Ranking, cap);
            var resume = database.Memory.Snapshot(MemoryScope.Resume, cap);
            return Ok(AiMemoryPayload.From(ranking, resume));
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost]
    public async Task<IActionResult> Verdict([FromQuery] long? jobid, [FromBody] AiVerdictRequest? body)
    {
        try
        {
            if (body == null)
                return BadRequest(new { error = "validation", message = "missing body" });

            if (!body.TryCreate(out var update, out var error))
                return BadRequest(new { error = "validation", message = error });

            var id = jobid ?? body.JobId;
            if (id is not long job_id)
                return BadRequest(new { error = "validation", message = "jobId is required" });

            var inventory = update.Delta == null ? null : await inventory_cache.GetAsync(HttpContext);
            if (!database.Job.ApplyAiVerdict(job_id, update, inventory))
                return NotFound();

            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    [HttpPost("/ai/run-report")]
    public IActionResult RunReport([FromBody] AiRunReportRequest? body)
    {
        try
        {
            if (body == null)
                return BadRequest(new { error = "validation", message = "missing body" });

            if (!body.TryCreate(out var run, out var error))
                return BadRequest(new { error = "validation", message = error });

            database.AiRun.Upsert(run);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }
}
