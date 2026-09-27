namespace Photon.JobSeeker;

public sealed record StatsFullResponse(DateTime GeneratedAt, StatsFilters Filters);

public sealed record StatsFilters(string[] Agencies, string[] Countries);
