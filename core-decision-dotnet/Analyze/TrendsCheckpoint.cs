namespace Photon.JobSeeker
{
    public class TrendsCheckpoint : IDisposable
    {
        private readonly Database database;
        private readonly Analyzer analyzer;
        private readonly Result result;

        public TrendsCheckpoint(Analyzer analyzer, Result? result = null)
        {
            this.analyzer = analyzer;
            this.result = result ?? new Result();
            database = Database.Open();
        }

        public Result CheckCurrentTrends()
        {
            LoadAndUpdateCurrentTrend();

            database.Trend.DeleteExpired(2);

            var all_current_trends = database.Trend.FetchAll()
                                                   .ToDictionary(k => (k.AgencyID, k.Type));

            var new_trends = CheckingSleptTrends(all_current_trends);

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
            if (result.Agency.HasValue && result.Trend.HasValue)
            {
                var trend = database.Trend.GetByID(result.Trend.Value);
                if (trend != null)
                {
                    trend.LastActivity = DateTime.Now;
                    trend.AgencyID = result.Agency.Value;
                    trend.State = result.State;
                    database.Trend.Save(trend);
                    return;
                }
            }

            result.Trend = null;
        }

        private List<(Agency agency, TrendType type)> CheckingSleptTrends(Dictionary<(long, TrendType), Trend> all_current_trends)
        {
            var new_trends = new List<(Agency agency, TrendType type)>();

            foreach (var agency_handler in analyzer.Agencies)
                for (var type = TrendType.Search; type <= TrendType.Job; type++)
                {
                    all_current_trends.TryGetValue((agency_handler.Value.ID, type), out var trend);
                    var matched_analyzed_result = agency_handler.Value.ID == result.Agency && type == result.Type;

                    if (trend == null)
                    {
                        if (matched_analyzed_result)
                            result.Trend = GenerateANewTrend(result.Agency ?? 0, result.State).TrendID;

                        else if (agency_handler.Value.Link != null)
                        {
                            GenerateANewTrend(agency_handler.Value.ID, type.GetTrendState());
                            new_trends.Add((agency_handler.Value, type));
                            continue;
                        }
                    }
                    else if (type == TrendType.Job && matched_analyzed_result)
                    {
                        new_trends.Add((agency_handler.Value, type));
                        continue;
                    }

                    // When whe don't have type:'Searching', we don't continue in type:'Analyzing'
                    break;
                }

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
                        commands.Insert(0, Command.Open(agency.Link));
                        break;

                    case TrendType.Job:
                        var url = database.Job.GetFirstJob(agency.ID);
                        if (url == null) break;
                        else if (result.Agency == agency.ID && result.Type == type && result.Trend != null)
                        {
                            commands.Insert(0, Command.Go(url));
                            commands = commands.Where(c => c.page_action == PageAction.close)
                                               .ToList();
                        }
                        else commands.Insert(0, Command.Open(url));
                        break;
                }

            if (result.Trend == null && !commands.Any(c => c.page_action == PageAction.close))
                commands.Add(Command.Close());

            result.Commands = commands.ToArray();
        }
    }
}