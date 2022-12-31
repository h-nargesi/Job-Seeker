namespace Photon.JobSeeker
{
    public enum TrendState : byte
    {
        Auth = 0,
        Login = 1,
        Seeking = 2,
        Other = 3,
        Analyzing = 128,
        InternalApplying = 129,
        ExternalApplying = 130,
    }
}