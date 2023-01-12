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
                var result = GetTrends(database);

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
                using var database = Database.Open();
                return View("~/views/agencies.cshtml", GetAgencies(database));
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
                var jobs = database.Job.Fetch();
                var trends = GetTrends(database);
                var agencies = GetAgencies(database);

                return View("~/views/index.cshtml", new { Trends = trends, Jobs = jobs, Agencies = agencies });
            }
            catch (Exception ex)
            {
                Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                throw;
            }
        }

        private List<dynamic> GetTrends(Database database)
        {
            database.Trend.DeleteExpired();
            var result = database.Trend.Report();

            if (JobEligibilityHelper.CurrentRevaluationProcess != null)
                result.Add(JobEligibilityHelper.CurrentRevaluationProcess.GetReportObject());

            return result;
        }

        private dynamic[] GetAgencies(Database database)
        {
            var report = database.Agency.JobRateReport();
            var agencies = analyzer.Agencies;

            return report.Select(r => {
                    agencies.TryGetValue(r.Title, out Agency agency);
                    return new
                    {
                        AgencyID = r.AgencyID,
                        Name = r.Title,
                        SearchLink = agency?.SearchLink,
                        JobCount = r.JobCount,
                        Analyzed = r.Analyzed,
                        Accepted = r.Accepted,
                        AnalyzingRate = r.AnalyzingRate,
                        AcceptingRate = r.AcceptingRate,
                        Running = agency?.RunningSearchingMethodIndex,
                        Methods = agency?.SearchingMethodTitles
                    };
                })
                .OrderBy(r => r.AgencyID)
                .ToArray();

        }
    }
}