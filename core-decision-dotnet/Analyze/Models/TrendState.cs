namespace Photon.JobSeeker.Analyze.Models
{
    public enum TrendState : byte
    {
        Blocked = 0,
        Auth,
        Login,
        Seeking,
        Other,
        Analyzing,
        // InternalApplying,
        // ExternalApplying,
    }
}