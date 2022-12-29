namespace Photon.JobSeeker
{
    public class TrendsCheckpoint : IDisposable
    {
        private readonly Database database;
        private readonly Analyzer analyzer;
        private readonly Result result;
        private Dictionary<(long, TrendType), Trend> AllCurrentTrends;

        public TrendsCheckpoint(Analyzer analyzer, Result? result = null)
        {
            this.analyzer = analyzer;
            this.result = result ?? new Result();
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

            return result;
        }

        public void Dispose()
        {
            database.Dispose();
            GC.SuppressFinalize(this);
        }

        private void LoadAndUpdateCurrentTrend()
        {
            if (result.AgencyID.HasValue && result.TrendID.HasValue)
            {
                try
                {
                    database.BeginTransaction();
                    var trend = database.Trend.Get(result.AgencyID.Value, result.State.GetTrendType());
                    if (trend != null && result.TrendID == trend.TrendID)
                    {
                        trend.LastActivity = DateTime.Now;
                        database.Trend.Save(trend, TrendFilter.LastActivity);
                        database.Commit();
                        return;
                    }
                }
                finally { database.Rollback(); }
            }

            result.TrendID = null;
        }

        private List<(Agency agency, TrendType type)> CheckingSleptTrends()
        {
            var new_trends = new List<(Agency agency, TrendType type)>();

            foreach (var agency in analyzer.Agencies.Values)
            {
                new_trends.AddRange(CheckingSleptSearchingTrends(agency, out var do_break));

                if (do_break) continue;

                new_trends.AddRange(CheckingSleptJobTrends(agency));
            }

            return new_trends;
        }

        private Trend? MatchingWithAnalyzedResult(Agency agency, TrendType type, out bool matched_analyzed_result)
        {
            AllCurrentTrends.TryGetValue((agency.ID, type), out var trend);
            matched_analyzed_result = agency.ID == result.AgencyID && type == result.Type;
            return trend;
        }

        private List<(Agency agency, TrendType type)> CheckingSleptSearchingTrends(Agency agency, out bool do_break)
        {
            var new_trends = new List<(Agency agency, TrendType type)>();

            var trend = MatchingWithAnalyzedResult(agency, TrendType.Search, out var matched_analyzed_result);

            if (trend is null)
                if (matched_analyzed_result)
                    result.TrendID = GenerateANewTrend(result.AgencyID ?? 0, result.State).TrendID;
                
                else new_trends.Add((agency, TrendType.Search));
            
            else if (matched_analyzed_result && result.TrendID is null)
                new_trends.Add((agency, TrendType.None));

            if (trend is null)
                do_break = !(matched_analyzed_result && result.State > TrendState.Login);
            
            else do_break = trend.State <= TrendState.Login;

            return new_trends;
        }

        private List<(Agency agency, TrendType type)> CheckingSleptJobTrends(Agency agency)
        {
            var new_trends = new List<(Agency agency, TrendType type)>();

            var trend = MatchingWithAnalyzedResult(agency, TrendType.Job, out var matched_analyzed_result);

            if (trend is null)
            {
                if (matched_analyzed_result)
                    result.TrendID = GenerateANewTrend(result.AgencyID ?? 0, result.State).TrendID;

                new_trends.Add((agency, TrendType.Job));
            }
            else if (result.TrendID is not null)
                new_trends.Add((agency, TrendType.Job));
            
            else if (matched_analyzed_result)
                new_trends.Add((agency, TrendType.None));

            return new_trends;
        }

        private Trend GenerateANewTrend(long agency_id, TrendState state)
        {
            var trend = new Trend
            {
                AgencyID = agency_id,
                State = state
            };
            database.Trend.Save(trend);
            return trend;
        }

        private void InjectOpenCommandForNewTrends(IEnumerable<(Agency agency, TrendType type)> new_trends)
        {
            var commands = new List<Command>(result.Commands);

            foreach (var (agency, type) in new_trends)
                switch (type)
                {
                    case TrendType.Search:
                        if (agency.Link == null) break;
                        commands.Insert(0, Command.Open(agency.Link));
                        break;

                    case TrendType.Job:
                        var url = database.Job.GetFirstJob(agency.ID);
                        if (url == null) break;
                        else if (result.AgencyID == agency.ID && result.Type == type)
                        {
                            commands.Add(Command.Go(url));
                            commands = commands.Where(c => c.page_action != PageAction.close)
                                               .ToList();
                        }
                        else commands.Insert(0, Command.Open(url));
                        break;

                    case TrendType.None:
                        if (!commands.Any(c => c.page_action == PageAction.close))
                            commands.Add(Command.Close());
                        break;

                }

            result.Commands = commands.ToArray();
        }
    }
}