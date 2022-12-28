namespace Photon.JobSeeker
{
    public class Trend
    {
        public long TrendID { get; set; }

        public long AgencyID { get; set; }

        public DateTime LastActivity { get; set; } = DateTime.Now;

        public TrendState State { get; set; }

        public TrendType Type => State.GetTrendType();
    }
}