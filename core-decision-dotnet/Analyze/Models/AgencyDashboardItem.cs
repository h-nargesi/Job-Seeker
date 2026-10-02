namespace Photon.JobSeeker;

public record AgencyDashboardItem(long AgencyID, string Name, string? SearchLink, long JobCount,
    Dictionary<JobState, long> StateCounts, bool Seeking, bool Analyzing, int? Running,
    AgencyRegion[] Methods);
