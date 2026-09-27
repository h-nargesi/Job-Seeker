namespace Photon.JobSeeker;

class AiRunBusiness
{
    private readonly Database database;

    public AiRunBusiness(Database database) => this.database = database;

    public void Upsert(AiRun run)
    {
        database.Execute(Q_UPSERT, new
        {
            run.RunID,
            run.StartedUtc,
            run.FinishedUtc,
            run.ExitCode,
            run.Model,
            run.WorkerVersion,
            run.Temperature,
            run.Seed,
            run.RubricHash,
            run.RubricTailorHash,
            run.Jobs,
            run.Promoted,
            run.ErrorVerdicts,
            run.Gone404,
            run.Retries,
            run.LlmFailures,
            run.PromptTokens,
            run.CompletionTokens,
            run.MaxCallTokens,
            run.CallMs,
            run.MaxCallMs,
            run.WallSeconds,
            run.FinishReasonLength,
            run.TruncatedJobs,
            run.DroppedMemoryRows,
            run.ErrorJobIds,
        });
    }

    public List<AiRun> Recent(int limit = 30)
    {
        return database.Query<AiRun>(Q_RECENT, new { limit }).ToList();
    }

    private const string Q_UPSERT = @"
INSERT OR REPLACE INTO AiRun (
    RunID, StartedUtc, FinishedUtc, ExitCode, Model, WorkerVersion, Temperature, Seed,
    RubricHash, RubricTailorHash, Jobs, Promoted, ErrorVerdicts, Gone404,
    Retries, LlmFailures, PromptTokens, CompletionTokens, MaxCallTokens, CallMs, MaxCallMs,
    WallSeconds, FinishReasonLength, TruncatedJobs, DroppedMemoryRows, ErrorJobIds
) VALUES (
    @RunID, @StartedUtc, @FinishedUtc, @ExitCode, @Model, @WorkerVersion, @Temperature, @Seed,
    @RubricHash, @RubricTailorHash, @Jobs, @Promoted, @ErrorVerdicts, @Gone404,
    @Retries, @LlmFailures, @PromptTokens, @CompletionTokens, @MaxCallTokens, @CallMs, @MaxCallMs,
    @WallSeconds, @FinishReasonLength, @TruncatedJobs, @DroppedMemoryRows, @ErrorJobIds
)";

    private const string Q_RECENT = @"
SELECT * FROM AiRun
ORDER BY StartedUtc DESC, RunID DESC
LIMIT @limit";
}
