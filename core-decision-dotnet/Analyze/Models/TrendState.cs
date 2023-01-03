namespace Photon.JobSeeker.Analyze.Models
{
    public enum TrendState : byte
    {
        Blocked = 0,
        Auth,
        Login,
        Other,
        Seeking,
        Analyzing,
    }
}