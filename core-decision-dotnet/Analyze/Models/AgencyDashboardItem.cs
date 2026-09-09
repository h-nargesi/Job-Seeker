namespace Photon.JobSeeker;

public record AgencyDashboardItem(long AgencyID, string Name, string? SearchLink, long JobCount,
    long Analyzed, long Accepted, long Applied, long AnalyzingRate, long AcceptingRate,
    int? Running, Agency.SearchingMethod[] Methods);
