namespace Photon.JobSeeker;

public enum TrendState : byte
{
    Blocked = 0,
    Finished,
    Auth,
    Login,
    Other,
    Seeking,
    Analyzing,
}