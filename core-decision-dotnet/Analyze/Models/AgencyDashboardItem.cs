namespace Photon.JobSeeker;

public record AgencyDashboardItem(long AgencyID, string Name, string? SearchLink, long JobCount,
    long Analyzed, long Accepted, long Applied, long AnalyzingRate, long AcceptingRate,
    bool Seeking, bool Analyzing, int? Running, Agency.SearchingMethod[] Methods);
