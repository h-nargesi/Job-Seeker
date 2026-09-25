namespace Photon.JobSeeker
{
    [Serializable]
    public class AgencyWaitingContext
    {
        public string? Agency { get; set; }
        public int? Waiting { get; set; }
    }
}
