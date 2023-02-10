using System.Text.RegularExpressions;
using Serilog;

namespace Photon.JobSeeker
{
    public abstract class Agency
    {
        private int running_searching_method_index = -1;

        private readonly List<Page> pages = new();

        public const string SearchTitle = "developer";


        public long ID { get; private set; }

        public abstract string Name { get; }

        public string Domain { get; private set; } = "";

        public string Link { get; private set; } = "";

        public bool IsActiveSeeking { get; private set; }

        public bool IsActiveAnalyzing { get; private set; }

        public IReadOnlyList<Page> Pages => pages;

        public abstract Regex? JobAcceptabilityChecker { get; }


        public int RunningSearchingMethodIndex
        {
            get => running_searching_method_index;
            set
            {
                if (value < 0 || value >= SearchingMethodTitles.Length)
                    throw new ArgumentOutOfRangeException(nameof(RunningSearchingMethodIndex));

                running_searching_method_index = value;
            }
        }

        public abstract string[] SearchingMethodTitles { get; }

        public abstract string SearchLink { get; }


        public abstract string GetMainHtml(string html);

        public Result AnalyzeContent(string url, string content)
        {
            using var database = Database.Open();
            dynamic? settings = database.Agency.LoadSetting(ID);
            ChangeSettings(settings);

            Log.Information("Agency ({0}): AnalyzeContent -running={1}", Name, RunningSearchingMethodIndex);

            foreach (var page in Pages)
            {
                var commands = page.IssueCommand(url, content);

                if (commands != null)
                {
                    var trend_state = page.TrendState;

                    if (page.TrendState == TrendState.Seeking && commands.Length == 0)
                    {
                        if (settings != null)
                        {
                            if (RunningSearchingMethodIndex + 1 < SearchingMethodTitles.Length)
                            {
                                settings.running += 1;
                                commands = new Command[] { Command.Go(SearchLink) };
                            }
                            else
                            {
                                settings.running = 0;
                                trend_state = TrendState.Finished;
                            }

                            ChangeSettings(settings);
                            database.Agency.ChangeRunningMethod(this);
                        }
                        else
                        {
                            trend_state = TrendState.Finished;
                        }
                    }

                    Log.Information("Page checked: {0}, {1}", page.GetType().Name, trend_state);
                    Log.Debug("Page commands: {0}", commands.StringJoin());

                    return new Result { State = trend_state, Commands = commands };
                }
            }

            Log.Warning("Agency ({0}): Page not found", Name);
            return new Result();
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

            ChangeSettings(agency_info.Settings);

            LoadPages();
        }

        protected abstract void ChangeSettings(dynamic? settings);

        protected abstract IEnumerable<Type> GetSubPages();

        private void LoadPages()
        {
            pages.Clear();
            Log.Debug("loading pages of", Name);

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