using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Photon.JobSeeker.Analyze.Pages;
using Photon.JobSeeker.Pages;
using Serilog;

namespace Photon.JobSeeker;

public abstract class Agency
{
    private readonly object agency_lock = new();

    private AgencySetting settings = new();

    private List<Page> pages = [];

    private JobPage? jobPage;

    public const string SearchTitle = "developer";


    public long ID { get; private set; }

    internal IDatabaseFactory DatabaseFactory { get; set; } = null!;

    public abstract string Name { get; }

    public string Domain { get; private set; } = string.Empty;

    public string Link { get; private set; } = string.Empty;

    public virtual int DefaultWaiting => 8_000;

    public virtual ReadinessRule[] ReadinessRules => [];

    public virtual int DefaultPacing => 10_000;

    public int Pacing => settings.Waiting ?? DefaultPacing;

    public int? PacingOverride => settings.Waiting;

    public AgencyStatus Status { get; set; }

    public bool IsActiveSeeking => Status.HasFlag(AgencyStatus.ActiveSeeking);

    public bool IsActiveAnalyzing => Status.HasFlag(AgencyStatus.ActiveAnalyzing);

    public IReadOnlyList<Page> Pages => pages;

    public abstract Regex? JobAcceptabilityChecker { get; }


    public int CurrentMethodIndex
    {
        get => settings.Running;
        set => settings.SetRunningIndex(value);
    }

    public int SearchingMethodCount => settings.Length;

    public AgencyRegion CurrentMethod => settings.Current;

    public AgencyRegion[]? EnabledSearchingMethod => settings.EnabledMethods;

    public AgencyRegion[] AllSearchingMethod => settings.Methods ?? [];

    public abstract AgencyRegion ParseRegion(string url);

    public abstract string SearchLink { get; }


    public string GetMainHtml(string html)
    {
        if (jobPage == null)
        {
            Log.Error("Agency ({0}): no job page registered for content extraction", Name);
            return string.Empty;
        }

        return jobPage.GetHtmlContent(html);
    }

    public Result AnalyzeContent(string url, string content)
    {
        lock (agency_lock)
        {
            Log.Information("Agency ({0}): AnalyzeContent -running={1}", Name, CurrentMethodIndex);

            foreach (var page in Pages)
            {
                var commands = page.IssueCommand(url, content);

                if (commands != null)
                {
                    var trend_state = page.TrendState;

                    if (page.TrendState == TrendState.Seeking && commands.Length == 0)
                    {
                        if (CurrentMethodIndex + 1 < settings.Length)
                        {
                            CurrentMethodIndex += 1;
                            commands = [Command.Go(SearchLink)];
                        }
                        else
                        {
                            CurrentMethodIndex = 0;
                            trend_state = TrendState.Finished;
                            Status &= ~AgencyStatus.ActiveSeeking;
                        }

                        using var database = DatabaseFactory.Open();
                        database.Agency.SaveState(this);
                    }

                    Log.Information("Page checked: {0}, {1}", page.GetType().Name, trend_state);
                    Log.Debug("Page commands: {0}", commands.StringJoin());

                    return new Result { State = trend_state, Commands = commands };
                }
            }

            Log.Warning("Agency ({0}): Page not found", Name);
            return new Result();
        }
    }

    public void ApplyRunning(int running, Database database)
    {
        lock (agency_lock)
        {
            CurrentMethodIndex = running;
            Status |= AgencyStatus.ActiveSeeking;
            database.Trend.Clear(ID, TrendType.Search);
            database.Agency.SaveState(this);
        }
    }

    public void ApplyStatus(bool? seeking, bool? analyzing, Database database)
    {
        lock (agency_lock)
        {
            if (seeking.HasValue)
            {
                if (seeking.Value)
                {
                    if (!IsActiveSeeking)
                    {
                        Status |= AgencyStatus.ActiveSeeking;
                        database.Trend.Clear(ID, TrendType.Search);
                    }
                }
                else Status &= ~AgencyStatus.ActiveSeeking;
            }

            if (analyzing.HasValue)
            {
                if (analyzing.Value)
                {
                    if (!IsActiveAnalyzing)
                    {
                        Status |= AgencyStatus.ActiveAnalyzing;
                        database.Trend.Clear(ID, TrendType.Job);
                    }
                }
                else Status &= ~AgencyStatus.ActiveAnalyzing;
            }

            database.Agency.SaveState(this);
        }
    }

    public void ApplyWaiting(int? waiting, Database database)
    {
        lock (agency_lock)
        {
            settings.Waiting = waiting;
            database.Agency.SaveWaiting(this, waiting);
        }
    }

    public void LoadFromDatabase(Database database)
    {
        var agency_info = database.Agency.LoadByName(Name);
        if (agency_info == null) return;

        Status = (AgencyStatus)agency_info.Active;

        ID = agency_info.AgencyID;
        Domain = agency_info.Domain;
        Link = agency_info.Link.Trim();
        Link = Link.EndsWith('/') ? Link[..^1] : Link;

        LoadSettings(agency_info.Settings);

        LoadPages();
    }

    private void LoadSettings(AgencySetting? settings)
    {
        if (settings == null) return;

        lock (agency_lock)
        {
            this.settings = settings;
            this.settings.Check();
        }
    }

    protected abstract IEnumerable<Type> GetSubPages();

    private void LoadPages()
    {
        pages.Clear();
        Log.Debug("loading pages of", Name);

        foreach (var type in GetSubPages())
        {
            if (Activator.CreateInstance(type, this) is not Page page) continue;

            if (page is JobPage job_page) jobPage = job_page;

            pages.Add(page);
            Log.Debug("page added: {0}", type.Name);
        }

        pages.Sort();
        Log.Information("pages: {0}", pages.StringJoin());
    }

    public class AgencySetting
    {
        private AgencyRegion[]? all_methods = null;

        [JsonProperty("running")]
        public int Running { get; set; } = -1;

        [JsonProperty("methods")]
        public AgencyRegion[]? Methods
        {
            get => all_methods;
            set
            {
                all_methods = value;
                EnabledMethods = all_methods?.Where(m => m.Enabled != false).ToArray();
                if (EnabledMethods?.Length == 0) EnabledMethods = null;
            }
        }

        [JsonIgnore]
        public AgencyRegion[]? EnabledMethods { get; private set; }

        [JsonIgnore]
        public int Length => EnabledMethods?.Length ?? 0;

        [JsonProperty("waiting", NullValueHandling = NullValueHandling.Ignore)]
        public int? Waiting { get; set; }

        [JsonIgnore]
        public AgencyRegion Current
        {
            get => EnabledMethods?[Running] ?? AgencyRegion.Empty;
        }

        public void Check()
        {
            if (all_methods?.Length == 0)
            {
                Methods = null;
            }

            if (Running < 0 || Running >= Length)
            {
                Running = Length < 1 ? -1 : 0;
            }
        }

        public void SetRunningIndex(int index)
        {
            if (index < 0 || index >= Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            Running = index;
        }
    }
}
