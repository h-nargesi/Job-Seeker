namespace Photon.JobSeeker;

[Flags]
public enum AgencyStatus
{
    None = 0,
    ActiveSeeking = 1,
    ActiveAnalyzing = 2,
    Active = 3,
}