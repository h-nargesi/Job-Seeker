namespace Photon.JobSeeker;

public sealed record StatsFullResponse(
    DateTime GeneratedAt,
    StatsFilters Filters,
    List<StatsAgencyYieldItem>? AgencyYield,
    StatsFunnel? Funnel,
    List<PipelineHealthItem>? PipelineHealth);

public sealed record StatsFilters(string[] Agencies, string[] Countries);

public sealed record StatsAgencyYieldItem(
    long AgencyID,
    string Title,
    long JobCount,
    long Analyzed,
    long Accepted,
    long Applied,
    long AnalyzingRate,
    long AcceptingRate);

public sealed record StatsFunnel(StatsFunnelStage Overall, List<StatsFunnelAgency> Agencies);

public sealed record StatsFunnelStage(long Saved, long Analyzed, long Attention, long Applied);

public sealed record StatsFunnelAgency(string Title, StatsFunnelStage Stages);

public sealed record PipelineHealthItem(string Day, long AiPending, long AiError);
