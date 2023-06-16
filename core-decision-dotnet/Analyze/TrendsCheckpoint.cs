using Serilog;

namespace Photon.JobSeeker
{
    public class TrendsCheckpoint : IDisposable
    {
        private readonly Database database;
        private readonly Analyzer analyzer;
        private readonly Result result;
        private Dictionary<(long, TrendType), Trend> AllCurrentTrends;

        public TrendsCheckpoint(Analyzer analyzer)
        {
            this.analyzer = analyzer;
            this.result = new Result();
            database = Database.Open();
            AllCurrentTrends = new Dictionary<(long, TrendType), Trend>();
        }

        public TrendsCheckpoint(Analyzer analyzer, Result result)
        {
            this.analyzer = analyzer;
            this.result = result;
            database = Database.Open();
            AllCurrentTrends = new Dictionary<(long, TrendType), Trend>();
        }

        public Result CheckCurrentTrends()
        {
            LoadAndUpdateCurrentTrend();

            database.Trend.DeleteExpired();

            AllCurrentTrends = database.Trend.FetchAll()
                                            .ToDictionary(k => (k.AgencyID, k.Type));

            var new_trends = CheckingSleptTrends();

            InjectOpenCommandForNewTrends(new_trends);

            Log.Information("Trend final commands: {0}", result.Commands.StringJoin());

            return result;
        }

        public void Dispose()
        {
            database.Dispose();
            GC.SuppressFinalize(this);
        }

        private void LoadAndUpdateCurrentTrend()
        {
            if (result.AgencyID.HasValue)
                try
                {
                    database.BeginTransaction();
                    var trend = database.Trend.Get(result.AgencyID.Value, result.State.GetTrendType());
                    if (trend != null)
                    {
                        Log.Debug("Trend (id:{0}{4}) Agency({1}) {2}, {3}",
                            trend.TrendID, trend.AgencyID, trend.Type, trend.State,
                            result.TrendID.HasValue ? "" : "-reserved");

                        if (result.TrendID == trend.TrendID || !result.TrendID.HasValue && trend.Reserved)
                        {
                            trend.LastActivity = DateTime.Now;
                            trend.State = result.State;
                            trend.Reserved = false;
                            database.Trend.Save(trend, TrendFilter.All & ~TrendFilter.AgencyID);
                            database.Commit();

                            result.TrendID = trend.TrendID;
                            return;
                        }
                    }
                }
                finally { database.Rollback(); }

            Log.Debug("Trend (unknown) Agency({0}) {1}, {2}",
                result.AgencyID, result.Type, result.State);

            result.TrendID = null;
        }

        private List<(Agency agency, TrendType type)> CheckingSleptTrends()
        {
            TrendType? new_trend;
            var new_trends = new List<(Agency agency, TrendType type)>();

            foreach (var agency in analyzer.Agencies.Values)
            {
                new_trend = CheckingSleptLoginTrends(agency, out var go);
                if (new_trend != null)
                    new_trends.Add((agency, new_trend.Value));

                if (!go) continue;

                new_trend = CheckingSleptSearchingTrends(agency);
                if (new_trend != null)
                    new_trends.Add((agency, new_trend.Value));

                new_trend = CheckingSleptJobTrends(agency);
                if (new_trend != null)
                    new_trends.Add((agency, new_trend.Value));
            }

            return new_trends;
        }

        private TrendType? CheckingSleptLoginTrends(Agency agency, out bool go)
        {
            TrendType? new_trend = null;

            var trend = MatchingWithAnalyzedResult(agency, TrendType.Login, out var matched_analyzed_result);

            var had_not_trend = result.TrendID is null;

            if (trend is not null)
            {
                go = trend.State > TrendState.Login;

                if (matched_analyzed_result && had_not_trend)
                    new_trend = TrendType.Blocked;
            }
            else if (matched_analyzed_result)
            {
                go = result.State > TrendState.Login;

                result.TrendID = GenerateNewTrend(agency.ID, result.State).TrendID;
            }
            else go = true;

            LogCheckingSleptTrends(agency, TrendType.Login, trend, matched_analyzed_result, had_not_trend, new_trend);

            return new_trend;
        }

        private TrendType? CheckingSleptSearchingTrends(Agency agency)
        {
            TrendType? new_trend = null;

            var trend = MatchingWithAnalyzedResult(agency, TrendType.Search, out var matched_analyzed_result);

            var had_not_trend = result.TrendID is null;

            if (!agency.IsActiveSeeking)
            {
                if (trend is not null)
                    database.Trend.Block(trend.TrendID);
                else
                    database.Trend.Block(agency.ID, TrendType.Search);

                if (matched_analyzed_result)
                    new_trend = TrendType.Blocked;
            }
            else
            {
                if (trend is not null)
                {
                    if (matched_analyzed_result && had_not_trend)
                        new_trend = TrendType.Blocked;
                }
                else if (matched_analyzed_result)
                    result.TrendID = GenerateNewTrend(result.AgencyID ?? 0, result.State).TrendID;

                else new_trend = TrendType.Search;
            }

            LogCheckingSleptTrends(agency, TrendType.Search, trend, matched_analyzed_result, had_not_trend, new_trend);

            return new_trend;
        }

        private TrendType? CheckingSleptJobTrends(Agency agency)
        {
            TrendType? new_trend = null;

            var trend = MatchingWithAnalyzedResult(agency, TrendType.Job, out var matched_analyzed_result);

            var had_not_trend = result.TrendID is null;

            if (!agency.IsActiveAnalyzing)
            {
                if (trend is not null)
                    database.Trend.Block(trend.TrendID);
                else
                    database.Trend.Block(agency.ID, TrendType.Job);

                if (matched_analyzed_result)
                    new_trend = TrendType.Blocked;
            }
            else
            {
                if (trend is not null)
                {
                    if (matched_analyzed_result)
                    {
                        if (result.TrendID is not null)
                            new_trend = TrendType.Job;

                        else new_trend = TrendType.Blocked;
                    }
                }
                else
                {
                    if (matched_analyzed_result)
                        result.TrendID = GenerateNewTrend(result.AgencyID ?? 0, result.State).TrendID;

                    new_trend = TrendType.Job;
                }
            }

            LogCheckingSleptTrends(agency, TrendType.Job, trend, matched_analyzed_result, had_not_trend, new_trend);

            return new_trend;
        }

        private Trend? MatchingWithAnalyzedResult(Agency agency, TrendType type, out bool matched_analyzed_result)
        {
            AllCurrentTrends.TryGetValue((agency.ID, type), out var trend);
            matched_analyzed_result = agency.ID == result.AgencyID && type == result.Type;
            return trend;
        }

        private void InjectOpenCommandForNewTrends(IEnumerable<(Agency agency, TrendType type)> new_trends)
        {
            var closed = false;
            var commands = new List<Command>(result.Commands);
            var open_new_page_in_agencies = new HashSet<long>();

            foreach (var (agency, type) in new_trends)
                switch (type)
                {
                    case TrendType.Search:
                        if (open_new_page_in_agencies.Contains(agency.ID)) continue;
                        open_new_page_in_agencies.Add(agency.ID);

                        GenerateNewTrend(agency.ID, type.GetTrendState(), true);
                        commands.Insert(0, Command.Open(agency.SearchLink));
                        break;

                    case TrendType.Job:
                        var url = database.Job.GetFirstJob(agency.ID);
                        if (url == null) break;

                        if (result.AgencyID == agency.ID && result.Type == type)
                        {
                            commands.Add(Command.Go(url));
                            commands = commands.Where(c => c.page_action != PageAction.close)
                                               .ToList();
                        }
                        else
                        {
                            if (open_new_page_in_agencies.Contains(agency.ID)) continue;
                            open_new_page_in_agencies.Add(agency.ID);

                            GenerateNewTrend(agency.ID, type.GetTrendState(), true);
                            commands.Insert(0, Command.Open(url));
                        }
                        break;

                    case TrendType.Blocked:
                        if (!closed)
                        {
                            commands.Add(Command.Close());
                            closed = true;
                        }
                        break;

                }

            if (!commands.Any(c => c.page_action != PageAction.open))
                commands.Add(Command.Close());

            result.Commands = commands.ToArray();
        }

        private Trend GenerateNewTrend(long agency_id, TrendState state, bool reserved = false)
        {
            var trend = new Trend
            {
                AgencyID = agency_id,
                State = state,
                Reserved = reserved,
            };
            database.Trend.Save(trend);
            return trend;
        }

        private static void LogCheckingSleptTrends(Agency agency, TrendType type, Trend? trend,
            bool matched_analyzed_result, bool had_not_trend, TrendType? new_trend)
        {
            var active = type == TrendType.Search ? agency.IsActiveSeeking : agency.IsActiveAnalyzing;
            var type_str = type.ToString();

            Log.Debug("Trend ({0}-{1}) -self={2} -db-trend={3} -had-trend-id={4} -blocked={6} -order={5}",
                FillSpace(agency.Name, AgencyNameLength = Math.Max(AgencyNameLength, agency.Name.Length)),
                FillSpace(type_str, TrednTypeLength = Math.Max(TrednTypeLength, type_str.Length)),
                matched_analyzed_result ? "*" : " ",
                trend is null ? "no " : "yes",
                had_not_trend ? "no " : "yes",
                new_trend,
                active ? "no " : "yes");
        }

        private static int AgencyNameLength = 6;
        private static int TrednTypeLength = 6;

        private static string FillSpace(string text, int max = 6)
        {
            return string.Join("", text, new string(' ', max - text.Length));
        }
    }
}