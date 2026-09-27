namespace Photon.JobSeeker;

public sealed record StatsDailyResponse(
    DateTime GeneratedAt,
    int Days,
    List<DailyStackedItem>? DailyStacked,
    List<VelocityItem>? Velocity,
    StatsKpis? Kpis);

public sealed record DailyStackedItem(
    string Day,
    long Saved,
    long Revaluation,
    long GateRejected,
    long InAi,
    long Attention,
    long Applied,
    long Rejected);

public sealed record VelocityItem(string Day, long Applied, long Rejected);

public sealed record StatsKpis(
    double? AvgDispositionDays,
    long AttentionBacklog,
    double? AttentionAvgAgeDays);
