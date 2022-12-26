using Serilog;

public class Analyzer
{
    private readonly object lock_loader = new();
    private readonly Trend trend_handler;
    private readonly Dictionary<string, Agency> agencies = new();

    public Analyzer(Trend trend) => trend_handler = trend;

    public IReadOnlyDictionary<string, Agency> Agencies
    {
        get
        {
            if (agencies.Count == 0)
            {
                lock (lock_loader)
                {
                    LoadAgencies();
                }
            }

            return agencies;
        }
    }

    public Result Analyze(long? trend, string agency, string url, string content)
    {
        var result = AnalyzeContent(agency, url, content);
        result.Trend = trend;
        return trend_handler.CheckTrend(result);
    }

    public void ClearAgencies()
    {
        agencies.Clear();
    }

    private Result AnalyzeContent(string agency, string url, string content)
    {
        Log.Debug("Analyzer.Analyze: {0}", agency);

        if (Agencies.ContainsKey(agency))
        {
            // TODO: SaveHtmlContent(__file__, content);
            var agency_handler = Agencies[agency];
            var result = agency_handler.AnalyzeContent(url, content);
            result.Agency = agency_handler.ID;
            return result;
        }
        else Log.Error("{0} not found!", agency);

        return new Result { Agency = null, Commands = Command.JustClose() };
    }

    private void LoadAgencies()
    {
        agencies.Clear();
        Log.Debug("loading agencies");

        var types = TypeHelper.GetSubTypes(typeof(Agency));

        using var database = Database.Open();
        foreach (var type in types)
        {
            if (Activator.CreateInstance(type) is not Agency agency) continue;

            if (agency.Name is null) continue;

            agency.LoadFromDatabase(database);

            if (agency.ID == default) return;

            agencies.Add(agency.Name, agency);
            Log.Debug("agency added: {0}", agency.Name);
        }
    }
}