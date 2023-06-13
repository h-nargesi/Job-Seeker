using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Serilog;

namespace Photon.JobSeeker
{
    [Route("[controller]/[action]")]
    public class JobController : Controller
    {
        private readonly Analyzer analyzer;

        public JobController(Analyzer analyzer) => this.analyzer = analyzer;

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
        public async Task<IActionResult> Resume([FromQuery] long jobid)
        {
            try
            {
                var resume_generator = HttpContext.RequestServices.GetService<IViewRenderService>();
                if (resume_generator == null) throw new Exception("The 'IViewRenderService' is not initialized.");

                var context = new object();
                var result = await resume_generator.RenderToStringAsync("resume", context);
                var base64_content = Convert.ToBase64String(Encoding.UTF8.GetBytes(result));

                return Ok(new
                {
                    Name = "null",
                    Content = base64_content,
                });
            }
            catch (Exception ex)
            {
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                throw;
            }
        }
    }
}