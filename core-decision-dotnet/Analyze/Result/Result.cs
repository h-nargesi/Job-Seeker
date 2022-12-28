namespace Photon.JobSeeker
{
    public class Result
    {
        public long? Trend { get; set; }

        public long? Agency { get; set; }

        public TrendState State { get; set; }

        public Command[] Commands { get; set; } = Command.JustClose();

        public TrendType Type => State.GetTrendType();
    }
}