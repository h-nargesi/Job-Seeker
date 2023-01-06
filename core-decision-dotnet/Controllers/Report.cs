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
                database.Trend.DeleteExpired();
                var trends = database.Trend.Report();
                var jobs = database.Job.Fetch();
                var agencies = GetAgencies();

                return View("~/views/index.cshtml", new { Trends = trends, Jobs = jobs, Agencies = agencies });
            }
            catch (Exception ex)
            {
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                throw;
            }
        }

        private dynamic[] GetAgencies()
        {
            using var database = Database.Open();
            var report = database.Agency.JobRateReport();
            
            return analyzer.Agencies.Select(a =>
                {
                    report.TryGetValue(a.Value.ID, out dynamic? agency_report);
                    return new
                    {
                        AgencyID = a.Value.ID,
                        SearchLink = a.Value.SearchLink,
                        Name = a.Key,
                        Analyzed = agency_report?.Analyzed,
                        Accepted = agency_report?.Accepted,
                        JobCount = agency_report?.JobCount,
                        AnalyzingRate = agency_report?.AnalyzingRate,
                        AcceptingRate = agency_report?.AcceptingRate,
                        Running = a.Value.RunningSearchingMethodIndex,
                        Methods = a.Value.SearchingMethodTitles
                    };
                })
                .OrderBy(r => r.AgencyID)
                .ToArray();

        }
    }
}