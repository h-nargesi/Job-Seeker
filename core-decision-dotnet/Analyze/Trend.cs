namespace Photon.JobSeeker
{
    public class Trend
    {
        private readonly ILogger logger;

        public Trend(ILogger<Analyzer> logger) => this.logger = logger;

        public Result CheckTrend(Result result)
        {
            // load current trend from db

            // load other trends

            // generate trend if not exists

            return result;
        }
    }
}