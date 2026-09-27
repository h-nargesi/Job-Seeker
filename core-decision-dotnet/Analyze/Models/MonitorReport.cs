namespace Photon.JobSeeker;

public sealed record MonitorFieldCoverage(string Field, long Judged, long Missing, double MissingRate);

public sealed record MonitorCalibration(
    long Judged,
    long BandMismatches,
    long InvertedSalaries,
    long KeywordRichWithoutSkills,
    int Floor,
    List<MonitorFieldCoverage> Fields);

public sealed class MonitorQueueHealth
{
    public long AiPending { get; set; }

    public long AiError { get; set; }

    public DateTime? OldestPending { get; set; }

    public MonitorQueueHealth()
    {
    }

    public MonitorQueueHealth(long aiPending, long aiError, DateTime? oldestPending)
    {
        AiPending = aiPending;
        AiError = aiError;
        OldestPending = oldestPending;
    }
}

public sealed record MonitorStageRow(
    string Label,
    long Saved,
    long Revaluation,
    long NotApprovedRegex,
    long AiPending,
    long NotApprovedAI,
    long AIError,
    long Attention,
    long Rejected,
    long Applied,
    string RegexPassRate,
    string AiPromoteRate,
    string DispositionRate);

public sealed class MonitorVerdictDistribution
{
    public long StrongMatch { get; set; }

    public long Match { get; set; }

    public long Possible { get; set; }

    public long NoMatch { get; set; }

    public long Error { get; set; }

    public long Judged => StrongMatch + Match + Possible + NoMatch;

    public long AtLeastMatch => StrongMatch + Match;
}

public sealed class MonitorPendingAges
{
    public long To6Hours { get; set; }

    public long From6To24Hours { get; set; }

    public long From1To3Days { get; set; }

    public long Over3Days { get; set; }

    public long Total => To6Hours + From6To24Hours + From1To3Days + Over3Days;
}

public sealed record MonitorViewModel(
    List<AiRun> Runs,
    MonitorCalibration Calibration,
    MonitorQueueHealth Queue,
    List<MonitorStageRow> Stages,
    string Group,
    MonitorVerdictDistribution Verdicts,
    MonitorPendingAges PendingAges);
