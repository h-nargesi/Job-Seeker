using Serilog;

namespace Photon.JobSeeker
{
    public abstract class Agency
    {
        private readonly List<Page> pages = new();

        public abstract string Name { get; }

        public long ID { get; private set; }

        public string Domain { get; private set; } = "";

        public string Link { get; private set; } = "";

        public bool IsActiveSeeking { get; private set; }

        public bool IsActiveAnalyzing { get; private set; }

        public IReadOnlyList<Page> Pages => pages;

        public Result AnalyzeContent(string url, string content)
        {
            using var database = Database.Open();
            dynamic? settings = database.Agency.LoadSetting(ID);

            Log.Information("Agency ({0}): AnalyzeContent -running={1}", Name, settings?.running);
            if (settings != null) PrepareNewSettings(settings);

            foreach (var page in Pages)
            {
                var commands = page.IssueCommand(url, content);

                if (commands != null)
                {
                    Log.Information("Page checked: {0}", page.GetType().Name);
                    Log.Debug("Page commands: {0}", commands.StringJoin());
                    return new Result { State = page.TrendState, Commands = commands };
                }
            }

            Log.Warning("Agency ({0}): Page not found", Name);
            return new Result { Commands = Command.JustClose() };
        }

        public void LoadFromDatabase(Database database)
        {
            var agency_info = database.Agency.LoadByName(Name);
            if (agency_info == null) return;

            var Active = agency_info.Active;
            if ((Active & 3) == 0) return;
            
            IsActiveSeeking = (Active & 1) == 1;
            IsActiveAnalyzing = (Active & 2) == 2;

            ID = agency_info.AgencyID;
            Domain = agency_info.Domain;
            Link = agency_info.Link;

            LoadPages();
        }

        protected abstract void PrepareNewSettings(dynamic setting);

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