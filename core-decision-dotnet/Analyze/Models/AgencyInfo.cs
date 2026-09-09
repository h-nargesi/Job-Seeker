namespace Photon.JobSeeker;

public record AgencyInfo(long AgencyID, string Domain, string Link, long Active, Agency.AgencySetting? Settings);
