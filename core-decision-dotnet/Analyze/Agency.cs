using Serilog;

namespace Photon.JobSeeker
{
    public abstract class Agency
    {
        private readonly List<Page> pages = new();

        public abstract string Name { get; }

        public long ID { get; private set; }

        public string? Domain { get; private set; }

        public IReadOnlyList<Page> Pages => pages;

        public Result AnalyzeContent(string url, string content)
        {
            Log.Debug("AnalyzeContent: {0}", Name);

            foreach (var page in Pages)
            {
                var commands = page.IssueCommand(url, content);

                if (commands != null)
                {
                    Log.Information("page checked: {0}", page.GetType().Name);
                    Log.Debug("page commands: {0}", commands.StringJoin());
                    return new Result { Type = page.TrendType, Commands = commands };
                }
            }

            Log.Debug("Page not found: {0}", Name);
            return new Result { Type = TrendType.None, Commands = Command.JustClose() };
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
            Log.Debug("loading pages of", Name);

            var types = GetSubPages();

            foreach (var type in GetSubPages())
            {
                if (Activator.CreateInstance(type, this) is not Page page) continue;

                pages.Add(page);
                Log.Debug("page added: {0}", type.Name);
            }

            pages.Sort();
            Log.Information("pages: {0}", pages.StringJoin());
        }
    }
}