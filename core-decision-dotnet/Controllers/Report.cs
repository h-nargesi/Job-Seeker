using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker
{
    [Route("[controller]/[action]")]
    public class ReportController : Controller
    {
        [HttpGet]
        public IActionResult Trends()
        {
            try
            {
                using var database = Database.Open();
                database.Trend.DeleteExpired();
                var result = database.Trend.Report();
                return View("~/views/trends.cshtml", result);
            }
            catch (Exception ex)
            {
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                throw;
            }
        }

        [HttpGet]
        public IActionResult Jobs()
        {
            try
            {
                using var database = Database.Open();
                var list = database.Job.Fetch();

                return View("~/views/jobs.cshtml", list);
            }
            catch (Exception ex)
            {
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                throw;
            }
        }

        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                using var database = Database.Open();
                database.Trend.DeleteExpired();
                var trends = database.Trend.Report();
                var jobs = database.Job.Fetch();

                return View("~/views/index.cshtml", new { Trends = trends, Jobs = jobs });
            }
            catch (Exception ex)
            {
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                throw;
            }
        }
    }
}