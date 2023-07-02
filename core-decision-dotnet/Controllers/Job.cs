using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Text;

namespace Photon.JobSeeker
{
    [Route("[controller]/[action]")]
    public class JobController : Controller
    {
        private readonly Analyzer analyzer;

        public JobController(Analyzer analyzer) => this.analyzer = analyzer;

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
                return Ok();
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

        [HttpPost]
        public IActionResult Setting([FromBody] string options)
        {
            try
            {
                using var database = Database.Open();
                database.Execute(options);
                return Ok();
            }
            catch (Exception ex)
            {
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                throw;
            }
        }

    }
}