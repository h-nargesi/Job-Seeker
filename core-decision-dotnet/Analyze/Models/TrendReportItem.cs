namespace Photon.JobSeeker;

public record TrendReportItem(long? TrendID, string Agency, string Link, string LastActivity,
    string? Type, string? State);
