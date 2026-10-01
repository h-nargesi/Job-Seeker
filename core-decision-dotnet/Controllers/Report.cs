using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Photon.JobSeeker;

[Route("[controller]/[action]")]
public partial class ReportController(Analyzer analyzer, Database database) : Controller
{
    private readonly Analyzer analyzer = analyzer;
    private readonly Database database = database;

    [HttpGet]
    public IActionResult Trends()
    {
        try
        {
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
            var jobs = GetJobs(database, null, null);
            var trends = GetTrends(database);
            var agencies = GetAgencies(database);

            return View("~/views/index.cshtml", new DashboardViewModel(trends, jobs, agencies));
        }
        catch (Exception ex)
        {
            Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
            throw;
        }
    }

    private static List<JobListItem> GetJobs(Database database, string? agencies, string? countries)
    {
        var agency_titles = agencies?.Split(',')
            .Where(id => !string.IsNullOrEmpty(id))
            .ToArray() ?? [];

        var country_codes = countries?.Split(',')
            .Where(id => !string.IsNullOrEmpty(id))
            .ToArray() ?? [];

        return database.Job.Fetch(agency_titles, country_codes);
    }

    private static List<TrendReportItem> GetTrends(Database database)
    {
        var result = database.Trend.Report();

        if (JobEligibilityHelper.CurrentRevaluationProcess != null)
            result.Add(JobEligibilityHelper.CurrentRevaluationProcess.GetReportObject());

        return result;
    }

    private AgencyDashboardItem[] GetAgencies(Database database)
    {
        return database.Agency.JobStateReport()
            .GroupBy(r => r.AgencyID)
            .Select(g =>
            {
                analyzer.AgenciesByID.TryGetValue(g.Key, out Agency? agency);
                var counts = new Dictionary<JobState, long>();
                foreach (var row in g)
                {
                    if (row.State != null && Enum.TryParse<JobState>(row.State, out var state))
                        counts[state] = counts.TryGetValue(state, out var count) ? count + row.Jobs : row.Jobs;
                }
                return new AgencyDashboardItem(
                    g.Key,
                    Name: g.First().Title,
                    SearchLink: agency?.SearchLink,
                    JobCount: counts.Values.Sum(),
                    StateCounts: counts,
                    Seeking: agency != null && agency.IsActiveSeeking,
                    Analyzing: agency != null && agency.IsActiveAnalyzing,
                    Running: agency == null ? null : agency.Status.HasFlag(AgencyStatus.ActiveSeeking) ? agency.CurrentMethodIndex : (int?)-1,
                    Methods: agency?.EnabledSearchingMethod ?? []);
            })
            .OrderBy(r => r.AgencyID)
            .ToArray();
    }
}
