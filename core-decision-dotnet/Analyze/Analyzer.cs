using Serilog;

namespace Photon.JobSeeker;

public class Analyzer
{
    private readonly object @lock = new();
    private readonly Dictionary<string, Agency> agencies_by_name = [];
    private readonly Dictionary<long, Agency> agencies_by_id = [];

    public IReadOnlyDictionary<string, Agency> Agencies
    {
        get
        {
            if (agencies_by_name.Count == 0)
            {
                lock (@lock)
                {
                    LoadAgencies();
                }
            }

            return agencies_by_name;
        }
    }

    public IReadOnlyDictionary<long, Agency> AgenciesByID
    {
        get
        {
            if (agencies_by_id.Count == 0)
            {
                lock (@lock)
                {
                    LoadAgencies();
                }
            }

            return agencies_by_id;
        }
    }

    public Result Analyze(PageContext context)
    {
        var result = AnalyzeContent(context);
        result.TrendID = context.Trend;
        using var trends_checkpoint = new TrendsCheckpoint(this, result);
        return trends_checkpoint.CheckCurrentTrends();
    }

    public void ClearAgencies()
    {
        agencies_by_name.Clear();
        agencies_by_id.Clear();
    }

    public void ReloadSettings()
    {
        using var database = Database.Open();
        lock (@lock)
        {
            foreach (var agency in agencies_by_id.Values)
            {
                agency.LoadFromDatabase(database);

                if (agency.IsActiveSeeking || agency.IsActiveAnalyzing)
                {
                    agencies_by_name[agency.Name] = agency;
                }
                else
                {
                    agencies_by_name.Remove(agency.Name);
                }

                Log.Debug("agency reloaded: {0} ({1}) - seeking={2}, analyzin={3}",
                    agency.Name, agency.ID, agency.IsActiveSeeking, agency.IsActiveAnalyzing);
            }
        }
    }

    private Result AnalyzeContent(PageContext context)
    {
        if (context.Agency == null)
            throw new BadJobRequest("Bad request (empty agency)");

        Log.Information("Analyze request: {0}", context.Agency);

        if (!Agencies.ContainsKey(context.Agency))
            throw new BadJobRequest($"{context.Agency} not found!");

        if (context.Url == null || context.Content == null)
            throw new BadJobRequest($"{context.Agency} had empty url/content");

        var agency = Agencies[context.Agency];
        var result = agency.AnalyzeContent(context.Url, context.Content);
        result.AgencyID = agency.ID;
        return result;
    }

    private void LoadAgencies()
    {
        agencies_by_name.Clear();
        agencies_by_id.Clear();
        Log.Debug("loading agencies");

        var types = TypeHelper.GetSubTypes(typeof(Agency));

        using var database = Database.Open();
        foreach (var type in types)
        {
            if (Activator.CreateInstance(type) is not Agency agency) continue;

            if (agency.Name is null) continue;

            agency.LoadFromDatabase(database);

            if (agency.ID == default) continue;

            if (agency.IsActiveSeeking || agency.IsActiveAnalyzing)
                agencies_by_name.Add(agency.Name, agency);
            agencies_by_id.Add(agency.ID, agency);
            Log.Debug("agency added: {0} ({1}) - seeking={2}, analyzin={3}",
                agency.Name, agency.ID, agency.IsActiveSeeking, agency.IsActiveAnalyzing);
        }
    }
}
