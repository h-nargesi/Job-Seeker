using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker
{
    [Route("[controller]/[action]")]
    public class JobController : Controller
    {
        [HttpPost]
        public IActionResult Revaluate()
        {
            try
            {
                Task.Run(() =>
                {
                    using var evaluator = new JobEligibilityHelper();
                    evaluator.Revaluate();
                });

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