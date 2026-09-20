namespace Photon.JobSeeker
{
    [Serializable]
    public class AgencyStatusContext
    {
        public string? Agency { get; set; }

        public bool? Seeking { get; set; }

        public bool? Analyzing { get; set; }
    }
}
