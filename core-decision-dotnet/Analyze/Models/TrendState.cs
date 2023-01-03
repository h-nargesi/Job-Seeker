namespace Photon.JobSeeker
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