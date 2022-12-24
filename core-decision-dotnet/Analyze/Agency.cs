public abstract class Agency
{
    private readonly List<Page> pages = new();
    public readonly ILogger logger;

    public Agency(ILogger<Agency> logger)
    {
        this.logger = logger;
    }

    public abstract string Name { get; }

    public long ID { get; private set; }

    public string? Domain { get; private set; }

    public IReadOnlyList<Page> Pages => pages;

    public Command[] AnalyzeContent(string url, string content)
    {
        logger.LogDebug("AnalyzeContent: {0}", Name);

        foreach (var page in Pages)
        {
            var commands = page.IssueCommand(url, content);

            if (commands != null)
            {
                logger.LogInformation("page checked: {0}", page.GetType().Name);
                logger.LogDebug("page commands: {0}", commands.StringJoin());
                return commands;
            }
        }

        logger.LogDebug("Page not found: {0}", Name);
        return new Command[] { Command.Close() };
    }

    public void LoadFromDatabase(Database database)
    {
        if (database == null) throw new ArgumentNullException(nameof(database));

        var agency_info = database.Agency.LoadByName(Name);
        if (agency_info == default) return;

        ID = agency_info.id;
        Domain = agency_info.domain;

        LoadPages();
    }

    protected abstract IEnumerable<Type> GetSubPages();

    private void LoadPages()
    {
        pages.Clear();
        logger.LogDebug("loading pages of", Name);

        var types = GetSubPages();

        foreach (var type in GetSubPages())
        {
            if (Activator.CreateInstance(type, this) is not Page page) continue;

            pages.Add(page);
            logger.LogDebug("page added: {0}", type.Name);
        }

        pages.Sort();
        logger.LogInformation("pages: {0}", pages.StringJoin());
    }
}