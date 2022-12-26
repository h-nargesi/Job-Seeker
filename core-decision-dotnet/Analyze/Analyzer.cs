using Serilog;

namespace Photon.JobSeeker
{
    public class Analyzer
    {
        private readonly object lock_loader = new();
        private readonly Trend trend_handler;
        private readonly Dictionary<string, Agency> agencies = new();

        public Analyzer(Trend trend) => trend_handler = trend;

        public IReadOnlyDictionary<string, Agency> Agencies
        {
            get
            {
                if (agencies.Count == 0)
                {
                    lock (lock_loader)
                    {
                        LoadAgencies();
                    }
                }

                return agencies;
            }
        }

        public Result Analyze(PageContext context)
        {
            var result = AnalyzeContent(context);
            result.Trend = context.Trend;
            return trend_handler.CheckTrend(result);
        }

        public void ClearAgencies()
        {
            agencies.Clear();
        }

        private Result AnalyzeContent(PageContext context)
        {
            if (context.Agency == null)
                throw new BadJobRequest("Bad request (empty agency)");

            Log.Debug("Analyzer.Analyze: {0}", context.Agency);

            if (!Agencies.ContainsKey(context.Agency))
                throw new BadJobRequest($"{context.Agency} not found!");

            if (context.Url == null || context.Content == null)
                throw new BadJobRequest($"{context.Agency} had empty url/content");

            var agency_handler = Agencies[context.Agency];
            var result = agency_handler.AnalyzeContent(context.Url, context.Content);
            result.Agency = agency_handler.ID;
            return result;
        }

        private void LoadAgencies()
        {
            agencies.Clear();
            Log.Debug("loading agencies");

            var types = TypeHelper.GetSubTypes(typeof(Agency));

            using var database = Database.Open();
            foreach (var type in types)
            {
                if (Activator.CreateInstance(type) is not Agency agency) continue;

                if (agency.Name is null) continue;

                agency.LoadFromDatabase(database);

                if (agency.ID == default) return;

                agencies.Add(agency.Name, agency);
                Log.Debug("agency added: {0}", agency.Name);
            }
        }
    }
}