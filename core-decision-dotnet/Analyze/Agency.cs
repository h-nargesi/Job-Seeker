using System.Text.RegularExpressions;
using Photon.JobSeeker.Analyze.Pages;
using Photon.JobSeeker.Pages;
using Serilog;

namespace Photon.JobSeeker;

public abstract class Agency
{
    private static readonly object @lock = new();

    private AgencySetting settings = new();

    private List<Page> pages = [];

    private JobPage? jobPage;

    public const string SearchTitle = "developer";


    public long ID { get; private set; }

    public abstract string Name { get; }

    public string Domain { get; private set; } = string.Empty;

    public string Link { get; private set; } = string.Empty;

    public virtual int Waiting => 0;

    public AgencyStatus Status { get; set; }

    public bool IsActiveSeeking => Status.HasFlag(AgencyStatus.ActiveSeeking);

    public bool IsActiveAnalyzing => Status.HasFlag(AgencyStatus.ActiveAnalyzing);

    public IReadOnlyList<Page> Pages => pages;

    public abstract Regex? JobAcceptabilityChecker { get; }


    public int CurrentMethodIndex
    {
        get => settings.Running;
        set
        {
            settings.SetRunningIndex(value);
            RunningSearchingMethodChanged(settings.Running);
        }
    }

    public int SearchingMethodCount => settings.Length;

    public SearchingMethod CurrentMethod => settings.Current;

    public SearchingMethod[]? EnabledSearchingMethod => settings.EnabledMethods;

    public virtual string BaseUrl => Link;

    public abstract string SearchLink { get; }


    public string GetMainHtml(string html)
    {
        return jobPage?.GetHtmlContent(html) ?? string.Empty;
    }

    public Result AnalyzeContent(string url, string content)
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

                    using var database = Database.Open();
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

    public void LoadFromDatabase(Database database)
    {
        var agency_info = database.Agency.LoadByName(Name);
        if (agency_info == null) return;

        Status = (AgencyStatus)(long)agency_info.Active;
        if (Status == AgencyStatus.None) return;

        ID = agency_info.AgencyID;
        Domain = agency_info.Domain;
        Link = agency_info.Link.Trim();
        Link = Link.EndsWith('/') ? Link[..^1] : Link;

        LoadSettings(agency_info.Settings);

        LoadPages();
    }

    private void LoadSettings(AgencySetting settings)
    {
        if (settings == null) return;

        lock (@lock)
        {
            this.settings = settings;
            this.settings.Check();
            RunningSearchingMethodChanged(this.settings.Running);
        }
    }

    protected abstract void RunningSearchingMethodChanged(int value);

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
        private SearchingMethod[]? all_methods = null;

        public int Running { get; set; } = -1;

        public SearchingMethod[]? Methods
        {
            get => all_methods;
            set
            {
                all_methods = value;
                EnabledMethods = all_methods?.Where(m => m.Enabled != false).ToArray();
                if (EnabledMethods?.Length == 0) EnabledMethods = null;
            }
        }

        public SearchingMethod[]? EnabledMethods { get; private set; }

        public int Length => EnabledMethods?.Length ?? 0;


        public SearchingMethod Current
        {
            get => EnabledMethods?[Running] ?? SearchingMethod.Empty;
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

    public struct SearchingMethod
    {
        public string Title { get; set; }

        public string Url { get; set; }

        public object? Params { get; set; }

        public bool? Enabled { get; set; }

        public static SearchingMethod Empty { get; } = new()
        {
            Title = string.Empty,
            Url = string.Empty,
            Params = null,
            Enabled = false,
        };
    }
}