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
            run.CallMs,
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
    RunID, StartedUtc, FinishedUtc, ExitCode, Model, Temperature, Seed,
    RubricHash, RubricTailorHash, Jobs, Promoted, ErrorVerdicts, Gone404,
    Retries, LlmFailures, PromptTokens, CompletionTokens, CallMs, WallSeconds,
    FinishReasonLength, TruncatedJobs, DroppedMemoryRows, ErrorJobIds
) VALUES (
    @RunID, @StartedUtc, @FinishedUtc, @ExitCode, @Model, @Temperature, @Seed,
    @RubricHash, @RubricTailorHash, @Jobs, @Promoted, @ErrorVerdicts, @Gone404,
    @Retries, @LlmFailures, @PromptTokens, @CompletionTokens, @CallMs, @WallSeconds,
    @FinishReasonLength, @TruncatedJobs, @DroppedMemoryRows, @ErrorJobIds
)";

    private const string Q_RECENT = @"
SELECT * FROM AiRun
ORDER BY StartedUtc DESC, RunID DESC
LIMIT @limit";
}
