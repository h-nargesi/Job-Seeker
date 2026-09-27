namespace Photon.JobSeeker;

public sealed record StatsFullResponse(
    DateTime GeneratedAt,
    StatsFilters Filters,
    List<StatsAgencyYieldItem>? AgencyYield,
    StatsFunnel? Funnel,
    List<PipelineHealthItem>? PipelineHealth,
    StatsSkillsGap? SkillsGap);

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

public sealed record StatsSkillsGap(long Jobs, List<SkillsGapItem> Top);

public sealed record SkillsGapItem(string Skill, long Jobs, bool Have);
