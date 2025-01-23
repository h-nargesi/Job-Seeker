using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker;

[Route("[controller]/[action]")]
public class ReportController(Analyzer analyzer) : Controller
{
    private readonly Analyzer analyzer = analyzer;

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
    public IActionResult Jobs(string? agencies, string? countries)
    {
        try
        {
            using var database = Database.Open();
            var list = GetJobs(database, agencies, countries);

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

    [HttpGet("/")]
    public IActionResult Index()
    {
        try
        {
            using var database = Database.Open();
            var jobs = GetJobs(database, null, null);
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

    private static List<dynamic> GetJobs(Database database, string? agencies, string? countries)
    {
        var agency_titles = agencies?.Split(',')
            .Where(id => !string.IsNullOrEmpty(id))
            .ToArray() ?? [];

        var country_codes = countries?.Split(',')
            .Where(id => !string.IsNullOrEmpty(id))
            .ToArray() ?? [];

        return database.Job.Fetch(agency_titles, country_codes);
    }

    private static List<dynamic> GetTrends(Database database)
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

        return report.Select(r =>
            {
                agencies.TryGetValue(r.Title, out Agency agency);
                return new
                {
                    r.AgencyID,
                    Name = r.Title,
                    agency?.SearchLink,
                    r.JobCount,
                    r.Analyzed,
                    r.Accepted,
                    r.Applied,
                    r.AnalyzingRate,
                    r.AcceptingRate,
                    Running = agency == null ? null : agency.Status.HasFlag(AgencyStatus.ActiveSeeking) ? agency.CurrentMethodIndex : (int?)-1,
                    Methods = agency?.EnabledSearchingMethod ?? []
                };
            })
            .OrderBy(r => r.AgencyID)
            .ToArray();

    }
}
