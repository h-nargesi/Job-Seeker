namespace AiWorker;

public sealed class WorkerLoop
{
    public const int ExitOk = 0;
    public const int ExitCoreAbort = 1;
    public const int ExitLlmUnavailable = 2;
    public const int ExitInterrupted = 3;

    public const int MaxModelAttempts = 2;

    private readonly CoreClient core;
    private readonly LlmClient llm;
    private readonly PromptBuilder prompt;

    public WorkerLoop(CoreClient core, LlmClient llm, PromptBuilder prompt)
    {
        this.core = core;
        this.llm = llm;
        this.prompt = prompt;
    }

    public async Task<int> RunAsync(CancellationToken ct)
    {
        while (true)
        {
            AiNextPayload next;
            try
            {
                next = await core.FetchNextAsync(ct);
            }
            catch (CoreAbortException ex)
            {
                return Abort(ExitCoreAbort, $"core error, aborting run: {ex.Message}");
            }

            if (next.Empty)
            {
                Console.WriteLine("[worker] queue empty - run complete");
                return ExitOk;
            }

            if (next.JobId is not long job_id || next.Content is null || next.Resume is null || next.Fingerprint is null)
                return Abort(ExitCoreAbort, "core error, aborting run: /ai/next payload is missing jobId/content/resume/fingerprint");

            VerdictPayload verdict;
            try
            {
                verdict = await JudgeAsync(next, job_id, ct);
            }
            catch (LlmUnavailableException ex)
            {
                return Abort(ExitLlmUnavailable,
                    $"model endpoint unavailable for job {job_id}, aborting run (nothing posted): {ex.Message}");
            }

            try
            {
                var outcome = await core.PostVerdictAsync(job_id, verdict, ct);
                if (outcome == PostVerdictResult.JobMissing)
                    Console.WriteLine($"[worker] job {job_id}: gone (404) - continuing");
                else
                    Console.WriteLine($"[worker] job {job_id}: verdict {verdict.Verdict} ({verdict.Relevance}) - posted");
            }
            catch (CoreAbortException ex)
            {
                return Abort(ExitCoreAbort, $"core error, aborting run: {ex.Message}");
            }
        }
    }

    private async Task<VerdictPayload> JudgeAsync(AiNextPayload next, long jobId, CancellationToken ct)
    {
        var message = prompt.Compose(next);
        Exception? last = null;

        for (var attempt = 1; attempt <= MaxModelAttempts; attempt++)
        {
            try
            {
                var content = await llm.CompleteAsync(message.System, message.User, ct);
                return VerdictParser.Parse(content, jobId, next.Fingerprint!);
            }
            catch (LlmUnavailableException)
            {
                throw;
            }
            catch (ModelOutputException ex)
            {
                last = ex;
                Console.WriteLine($"[worker] job {jobId}: model output invalid (attempt {attempt}/{MaxModelAttempts}): {ex.Message}");
            }
        }

        Console.WriteLine($"[worker] job {jobId}: posting Error verdict after {MaxModelAttempts} model-output failures");
        return VerdictPayload.Error(jobId, next.Fingerprint!, last!.Message);
    }

    private static int Abort(int code, string message)
    {
        Console.Error.WriteLine($"[worker] {message}");
        return code;
    }
}
