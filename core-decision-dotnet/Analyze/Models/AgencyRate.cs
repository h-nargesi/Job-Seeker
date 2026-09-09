namespace Photon.JobSeeker;

public record AgencyRate(long AgencyID, string Title, long JobCount, long Analyzed, long Accepted,
    long Applied, long AnalyzingRate, long AcceptingRate);
