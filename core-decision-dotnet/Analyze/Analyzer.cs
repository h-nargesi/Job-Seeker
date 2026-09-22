using Serilog;

namespace Photon.JobSeeker;

public class Analyzer(IDatabaseFactory database_factory)
{
    private readonly object @lock = new();
    private readonly Dictionary<string, Agency> agencies_by_name = [];
    private readonly Dictionary<long, Agency> agencies_by_id = [];
    private bool loaded;

    internal IDatabaseFactory DatabaseFactory => database_factory;

    public IReadOnlyDictionary<string, Agency> Agencies
    {
        get
        {
            lock (@lock)
            {
                if (!loaded) LoadAgencies();
                return agencies_by_name;
            }
        }
    }

    public IReadOnlyDictionary<long, Agency> AgenciesByID
    {
        get
        {
            lock (@lock)
            {
                if (!loaded) LoadAgencies();
                return agencies_by_id;
            }
        }
    }

    public Agency? FindAgency(string name)
    {
        lock (@lock)
        {
            if (!loaded) LoadAgencies();
            return agencies_by_id.Values.FirstOrDefault(a => a.Name == name);
        }
    }

    public Result Analyze(PageContext context, Database database)
    {
        if (context.Challenge) return HoldChallenge(context, database);

        var result = AnalyzeContent(context);
        result.TrendID = context.Trend;
        var trends_checkpoint = new TrendsCheckpoint(this, database, result);
        return trends_checkpoint.CheckCurrentTrends();
    }

    public void ClearAgencies()
    {
        lock (@lock)
        {
            agencies_by_name.Clear();
            agencies_by_id.Clear();
            loaded = false;
        }
    }

    public void ReloadSettings()
    {
        using var database = database_factory.Open();
        lock (@lock)
        {
            if (!loaded)
            {
                LoadAgencies();
                return;
            }

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

    private Result HoldChallenge(PageContext context, Database database)
    {
        if (context.Agency == null)
            throw new BadJobRequest("Bad request (empty agency)");

        Log.Information("Challenge hold request: {0}", context.Agency);

        if (!Agencies.ContainsKey(context.Agency))
            throw new BadJobRequest($"{context.Agency} not found!");

        if (context.Url == null)
            throw new BadJobRequest($"{context.Agency} had empty url");

        var agency = Agencies[context.Agency];
        var result = new Result();
        var trends_checkpoint = new TrendsCheckpoint(this, database, result);
        return trends_checkpoint.HoldForChallenge(agency, context.Trend, context.Url);
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
        loaded = true;
        Log.Debug("loading agencies");

        var types = TypeHelper.GetSubTypes(typeof(Agency));

        using var database = database_factory.Open();
        foreach (var type in types)
        {
            if (Activator.CreateInstance(type) is not Agency agency) continue;

            if (agency.Name is null) continue;

            agency.DatabaseFactory = database_factory;
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
