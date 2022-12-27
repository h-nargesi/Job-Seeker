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
            var analyzed_trend = LoadAndUpdateCurrentTrend();

            database.Trend.DeleteExpired(2);

            var all_current_trends = database.Trend.FetchAll()
                                                   .ToDictionary(k => (k.AgencyID, k.Type));

            CheckingSleptTrends(all_current_trends, analyzed_trend);

            return result;
        }

        public void Dispose()
        {
            database.Dispose();
        }

        private Trend? LoadAndUpdateCurrentTrend()
        {
            if (result.Agency.HasValue && result.Trend.HasValue)
            {
                var trend = database.Trend.GetByID(result.Trend.Value);
                if (trend != null)
                {
                    trend.LastActivity = DateTime.Now;
                    trend.AgencyID = result.Agency.Value;
                    trend.Type = result.Type;
                    database.Trend.Save(trend);
                    return trend;
                }
            }

            result.Trend = null;
            return null;
        }

        private void CheckingSleptTrends(Dictionary<(long, TrendType), Trend> all_current_trends, Trend? analyzed_trend)
        {
            var commands = new List<Command>(result.Commands);

            foreach (var agency_handler in analyzer.Agencies)
                for (var type = TrendType.Searching; type <= TrendType.Analyzing; type++)
                {
                    all_current_trends.TryGetValue((agency_handler.Value.ID, type), out var trend);
                    var matched_analyzed_result = agency_handler.Value.ID == result.Agency && type == result.Type;

                    if (trend == null)
                    {
                        if (matched_analyzed_result)
                            result.Trend = GenerateANewTrend(result.Agency ?? 0, result.Type).TrendID;

                        else if (agency_handler.Value.Link != null)
                        {
                            var new_trend = GenerateANewTrend(agency_handler.Value.ID, type);
                            commands.Insert(0, Command.Open(agency_handler.Value.Link));
                        }
                    }
                }

            if (result.Trend == null && !commands.Any(c => c.page_action == PageAction.close))
                commands.Add(Command.Close());

            result.Commands = commands.ToArray();
        }

        private Trend GenerateANewTrend(long agency_id, TrendType type)
        {
            var trend = new Trend
            {
                AgencyID = agency_id,
                Type = type
            };
            database.Trend.Save(trend);
            return trend;
        }
    }
}