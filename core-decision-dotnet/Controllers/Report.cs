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
                database.Trend.DeleteExpired();
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
        public IActionResult Agencies()
        {
            try
            {
                return View("~/views/agencies.cshtml", GetAgencies());
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
                var list = database.Job.Fetch();

                return View("~/views/index.cshtml", list);
            }
            catch (Exception ex)
            {
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                throw;
            }
        }

        internal object GetAgencies()
        {
            return analyzer.Agencies.Select(a =>
                {
                    var result = a.Value.Runnables(out var current);
                    return new
                    {
                        Current = current,
                        Agencies = result
                    };
                })
                .ToArray();

        }
    }
}