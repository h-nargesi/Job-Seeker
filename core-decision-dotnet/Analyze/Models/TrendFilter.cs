namespace Photon.JobSeeker.Analyze.Models
{
    enum TrendFilter
    {
        AgencyID = 1,
        LastActivity = 2,
        Type = 4,
        State = 8,
        Reserved = 16,
        All = 31,
    }
}