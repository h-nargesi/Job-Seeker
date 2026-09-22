using System.Text.Json.Nodes;

namespace AiWorker;

public sealed class WorkerLoop
{
    public const int ExitOk = 0;
    public const int ExitCoreAbort = 1;
    public const int ExitLlmUnavailable = 2;
    public const int ExitInterrupted = 3;
    public const int ExitUnexpected = 5;

    public const int MaxModelAttempts = 2;

    private readonly CoreClient core;
    private readonly LlmClient llm;
    private readonly PromptBuilder prompt;
    private readonly LlmOptions? options;

    public WorkerLoop(CoreClient core, LlmClient llm, PromptBuilder prompt, LlmOptions? options = null)
    {
        this.core = core;
        this.llm = llm;
        this.prompt = prompt;
        this.options = options;
    }

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var stats = new RunStats();
        Banner();
        try
        {
            var code = await RunQueueAsync(stats, ct);
            Summary(code == ExitOk ? "complete" : "aborted", code, stats);
            return code;
        }
        catch (OperationCanceledException)
        {
            Summary("aborted", ExitInterrupted, stats);
            throw;
        }
        catch (Exception)
        {
            Summary("aborted", ExitUnexpected, stats);
            throw;
        }
    }

    private async Task<int> RunQueueAsync(RunStats stats, CancellationToken ct)
    {
        MemorySnapshot memory;
        try
        {
            memory = await core.FetchMemorySnapshotAsync(ct);
        }
        catch (CoreAbortException ex)
        {
            return Abort(ExitCoreAbort, $"core error, aborting run before memory snapshot: {ex.Message}");
        }

        var number = 0;
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
                WorkerLog.Info("queue empty - run complete");
                return ExitOk;
            }

            if (next.JobId is not long job_id || next.Content is null || next.Resume is null || next.Fingerprint is null)
                return Abort(ExitCoreAbort, "core error, aborting run: /ai/next payload is missing jobId/content/resume/fingerprint");

            number++;
            stats.Jobs++;
            WorkerLog.Info("job {JobId} #{Number}: content {Chars} chars, keywords {Keywords}, passmark {Passmark}",
                job_id, number, next.Content.Length, next.Keywords?.Count ?? 0,
                next.Settings?.Aipassmark.ToString() ?? "n/a");

            try
            {
                var verdict = await JudgeAsync(next, job_id, memory.Ranking, stats, ct);

                if (Promotes(next, verdict))
                {
                    stats.Promoted++;
                    verdict.Delta = await TailorAsync(next, job_id, memory.Resume, stats, ct);
                }

                if (verdict.Verdict == "Error") stats.ErrorVerdicts++;

                var outcome = await core.PostVerdictAsync(job_id, verdict, ct);
                if (outcome == PostVerdictResult.JobMissing)
                {
                    stats.Gone++;
                    WorkerLog.Info("job {JobId}: gone (404) - continuing", job_id);
                }
                else
                    WorkerLog.Info("job {JobId}: verdict {Verdict} ({Relevance}){Delta} - posted",
                        job_id, verdict.Verdict, verdict.Relevance,
                        verdict.Delta == null ? string.Empty : " + delta");
            }
            catch (LlmUnavailableException ex)
            {
                return Abort(ExitLlmUnavailable,
                    $"model endpoint unavailable for job {job_id}, aborting run (nothing posted): {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                WorkerLog.Warn("interrupted - job {JobId} in flight", job_id);
                throw;
            }
            catch (CoreAbortException ex)
            {
                return Abort(ExitCoreAbort, $"core error, aborting run: {ex.Message}");
            }
        }
    }

    internal static bool Promotes(AiNextPayload next, VerdictPayload verdict)
    {
        if (verdict.Verdict == "Error") return false;
        var passmark = next.Settings?.Aipassmark;
        return passmark is int mark && verdict.Relevance >= mark;
    }

    private async Task<VerdictPayload> JudgeAsync(AiNextPayload next, long jobId,
        IReadOnlyList<MemorySnapshotRow> rankingMemory, RunStats stats, CancellationToken ct)
    {
        var message = prompt.Compose(next, rankingMemory);
        LogPrompt(jobId, 1, message);
        Exception? last = null;

        for (var attempt = 1; attempt <= MaxModelAttempts; attempt++)
        {
            LlmResult? result = null;
            try
            {
                result = await llm.CompleteAsync(message.System, message.User, ct);
                LogCall(jobId, 1, result, stats);
                return VerdictParser.Parse(result.Content, jobId, next.Fingerprint!);
            }
            catch (LlmUnavailableException)
            {
                throw;
            }
            catch (ModelOutputException ex)
            {
                last = ex;
                stats.Retries++;
                WorkerLog.Warn("job {JobId}: model output invalid (attempt {Attempt}/{Attempts}): {Message}",
                    jobId, attempt, MaxModelAttempts, ex.Message);
                LogRawEvidence(jobId, 1, attempt, ex.Raw ?? result?.Content);
            }
        }

        WorkerLog.Error("job {JobId}: posting Error verdict after {Attempts} model-output failures",
            jobId, MaxModelAttempts);
        return VerdictPayload.Error(jobId, next.Fingerprint!, last!.Message);
    }

    private async Task<JsonNode?> TailorAsync(AiNextPayload next, long jobId,
        IReadOnlyList<MemorySnapshotRow> resumeMemory, RunStats stats, CancellationToken ct)
    {
        var message = prompt.ComposeTailor(next, resumeMemory);
        LogPrompt(jobId, 2, message);

        for (var attempt = 1; attempt <= MaxModelAttempts; attempt++)
        {
            LlmResult? result = null;
            try
            {
                result = await llm.CompleteAsync(message.System, message.User, VerdictSchema.TailorResponseFormat, ct);
                LogCall(jobId, 2, result, stats);
                return TailorParser.Parse(result.Content);
            }
            catch (LlmUnavailableException)
            {
                throw;
            }
            catch (ModelOutputException ex)
            {
                stats.Retries++;
                WorkerLog.Warn("job {JobId}: tailoring output invalid (attempt {Attempt}/{Attempts}): {Message}",
                    jobId, attempt, MaxModelAttempts, ex.Message);
                LogRawEvidence(jobId, 2, attempt, ex.Raw ?? result?.Content);
            }
        }

        WorkerLog.Error("job {JobId}: posting verdict without tailoring after {Attempts} model-output failures",
            jobId, MaxModelAttempts);
        return null;
    }

    private void Banner()
    {
        WorkerLog.Info("run starting: model {Model}, temperature {Temperature}, seed {Seed}",
            options?.Model ?? "n/a", options?.Temperature ?? LlmOptions.DefaultTemperature, options?.Seed ?? 0);
        WorkerLog.Info("endpoints: core {Core}, llm {Llm}; context {MaxContextTokens} tok, memory cap {MemoryTokenCap} tok",
            options?.Core ?? "n/a", options?.BaseUrl ?? "n/a",
            PromptBuilder.MaxContextTokens, PromptBuilder.MemoryTokenCap);
    }

    private static void LogPrompt(long jobId, int callNo, PromptBuilder.Prompt message)
    {
        WorkerLog.Info(
            "job {JobId} call {Call} prompt: system {SystemChars} chars (~{SystemTokens} tok), user {UserChars} chars (~{UserTokens} tok)",
            jobId, callNo, message.System.Length, message.EstSystemTokens,
            message.User.Length, message.EstContentTokens);
        WorkerLog.Debug("job {JobId} call {Call} system prompt body:{NewLine}{System}",
            jobId, callNo, Environment.NewLine, message.System);
        WorkerLog.Debug("job {JobId} call {Call} user prompt body:{NewLine}{User}",
            jobId, callNo, Environment.NewLine, message.User);

        if (message.ContentTruncatedChars > 0)
            WorkerLog.Info("job {JobId} call {Call}: JD truncated {Chars} chars (~{Tokens} tok est)",
                jobId, callNo, message.ContentTruncatedChars,
                message.ContentTruncatedChars / PromptBuilder.CharsPerToken);
        if (message.DroppedMemoryRows > 0)
            WorkerLog.Info("job {JobId} call {Call}: memory rows dropped {Dropped}/{Total}",
                jobId, callNo, message.DroppedMemoryRows, message.TotalMemoryRows);
    }

    private static void LogCall(long jobId, int callNo, LlmResult result, RunStats stats)
    {
        stats.Add(result);
        var rate = result.CompletionTokens is int completion && result.ElapsedMs > 0
            ? $"{completion / (result.ElapsedMs / 1000.0):0.0} tok/s"
            : "n/a";
        WorkerLog.Info(
            "job {JobId} call {Call}: {ElapsedMs} ms, in {PromptTokens} tok / out {CompletionTokens} tok, {Rate}, finish={Finish}",
            jobId, callNo, result.ElapsedMs,
            result.PromptTokens?.ToString() ?? "n/a",
            result.CompletionTokens?.ToString() ?? "n/a",
            rate, result.FinishReason ?? "n/a");
        if (result.FinishReason == "length")
            WorkerLog.Warn("job {JobId} call {Call}: finish_reason=length - output hit the context cap and was cut",
                jobId, callNo);
    }

    private static void LogRawEvidence(long jobId, int callNo, int attempt, string? raw)
    {
        if (raw is null) return;
        WorkerLog.Warn("job {JobId} call {Call} attempt {Attempt}: raw output: {Snippet}",
            jobId, callNo, attempt, WorkerLog.Snippet(raw));
        WorkerLog.WriteFailureDump(jobId, callNo, attempt, raw);
    }

    private static void Summary(string outcome, int code, RunStats stats)
    {
        WorkerLog.Info(
            "run {Outcome} exit {Code}: jobs {Jobs}, promoted {Promoted}, errors {Errors}, 404 {Gone}, retries {Retries}, wall {Wall:F1}s, avg {Avg:F1}s/job, tokens in {PromptTokens} / out {CompletionTokens}",
            outcome, code, stats.Jobs, stats.Promoted, stats.ErrorVerdicts, stats.Gone, stats.Retries,
            stats.WallSeconds, stats.AvgSecondsPerJob, stats.PromptTokens, stats.CompletionTokens);
    }

    private static int Abort(int code, string message)
    {
        WorkerLog.Error("{Message}", message);
        return code;
    }
}
