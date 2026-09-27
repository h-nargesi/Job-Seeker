namespace Photon.JobSeeker;

public sealed record StatsFullResponse(
    DateTime GeneratedAt,
    StatsFilters Filters,
    List<StatsAgencyYieldItem>? AgencyYield,
    StatsFunnel? Funnel,
    List<PipelineHealthItem>? PipelineHealth,
    StatsSkillsGap? SkillsGap,
    StatsScoreHistograms? ScoreHistograms,
    List<StatsDonutChart>? AiDonuts,
    StatsDonutChart? AiVerdictDonut,
    List<StatsBucketItem>? Competitiveness,
    StatsBucketList? AttentionAging,
    StatsDisposition? Disposition);

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

public sealed record StatsScoreHistograms(
    int AiPassmark,
    long RegexJobs,
    long AiJobs,
    List<string> Labels,
    List<long> RegexBins,
    List<long> AiBins);

public sealed record StatsDonutChart(string Key, string Title, List<StatsDonutSlice> Slices);

public sealed record StatsDonutSlice(string Label, long Jobs);

public sealed record StatsBucketItem(
    string Bucket,
    long Evaluated,
    long Passed,
    long PassRate,
    double? AvgEffectiveScore);

public sealed record StatsBucketList(long Total, List<StatsBucketCount> Buckets);

public sealed record StatsBucketCount(string Label, long Jobs);

public sealed record StatsDisposition(long Jobs, double? MedianDays, double? P90Days, List<StatsBucketCount> Buckets);
