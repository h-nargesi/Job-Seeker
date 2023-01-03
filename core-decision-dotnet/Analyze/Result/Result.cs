namespace Photon.JobSeeker
{
    public class Result
    {
        public long? TrendID { get; set; }

        public long? AgencyID { get; set; }

        public TrendState State { get; set; }

        public Command[] Commands { get; set; } = new Command[0];

        public TrendType Type => State.GetTrendType();
    }
}