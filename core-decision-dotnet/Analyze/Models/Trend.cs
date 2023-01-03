namespace Photon.JobSeeker
{
    public class Trend
    {
        public long TrendID { get; set; }

        public long AgencyID { get; set; }

        public TrendState State { get; set; }

        public TrendType Type => State.GetTrendType();

        public DateTime LastActivity { get; set; } = DateTime.Now;

        public bool Reserved { get; set; }
    }
}