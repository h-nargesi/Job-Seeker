public class Analyzer
{
    private readonly object lock_loader = new();
    private readonly ILogger logger;
    private readonly Dictionary<string, Agency> agencies = new();

    public Analyzer(ILogger<Analyzer> logger)
    {
        this.logger = logger;
    }

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

    public Command[] Analyze(string agency, string url, string content)
    {
        logger.LogDebug("Analyzer.Analyze: {0}", agency);

        if (Agencies.ContainsKey(agency))
        {
            // TODO: SaveHtmlContent(__file__, content);
            return Agencies[agency].AnalyzeContent(url, content);
        }
        else logger.LogError("{0} not found!", agency);

        return Array.Empty<Command>();
    }


    private void LoadAgencies()
    {
        agencies.Clear();
        logger.LogDebug("loading agencies");

        var types = AppDomain.CurrentDomain.GetAssemblies()
                                           .SelectMany(s => s.GetTypes())
                                           .Where(p => typeof(Agency).IsAssignableFrom(p));

        using var database = Database.Open();
        foreach (var type in types)
        {
            if (Activator.CreateInstance(type) is not Agency agency) continue;

            if (agency.Name is null) continue;

            agency.LoadFromDatabase(database);

            if (agency.ID == default) return;

            agencies.Add(agency.Name, agency);
            logger.LogDebug("agency added: {0}", agency.Name);
        }
    }
}