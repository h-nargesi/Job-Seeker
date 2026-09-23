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

public sealed record MonitorViewModel(
    List<AiRun> Runs,
    MonitorCalibration Calibration,
    MonitorQueueHealth Queue);
