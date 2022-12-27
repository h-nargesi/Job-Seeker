using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker
{
    [Route("[controller]/[action]")]
    public class ReportController : Controller
    {
        private readonly Analyzer analyzer;

        public ReportController(Analyzer analyzer) => this.analyzer = analyzer;

        [HttpGet]
        public IActionResult Trends()
        {
            try
            {
                using var database = Database.Open();
                var result = database.Trend.Report();
                return Ok(result);
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
                var list = database.Job.Fetch(JobState.attention);

                return View("~/views/index.cshtml", list);
            }
            catch (Exception ex)
            {
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                throw;
            }
        }
    }
}