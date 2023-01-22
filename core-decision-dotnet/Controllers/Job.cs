using Microsoft.AspNetCore.Mvc;
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
    }
}