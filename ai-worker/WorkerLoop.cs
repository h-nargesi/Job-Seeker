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
    public const int MaxConsecutiveLlmFailures = 3;

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
        try
        {
            var code = await RunQueueAsync(stats, ct);
            Summary(code == ExitOk ? "complete" : "aborted", code, stats);
            await PostReportAsync(code, stats);
            return code;
        }
        catch (OperationCanceledException)
        {
            Summary("aborted", ExitInterrupted, stats);
            await PostReportAsync(ExitInterrupted, stats);
            throw;
        }
        catch (Exception)
        {
            Summary("aborted", ExitUnexpected, stats);
            await PostReportAsync(ExitUnexpected, stats);
            throw;
        }
    }

    private async Task PostReportAsync(int code, RunStats stats)
    {
        try
        {
            await core.PostRunReportAsync(RunReport.From(WorkerLog.RunId, code, options, stats));
        }
        catch (Exception ex)
        {
            WorkerLog.Warn("run report POST failed: {Message}", ex.Message);
        }
    }

    private sealed record TrunkContext(PromptBuilder.Trunk Trunk, string? Version, int Passmark);

    private async Task<TrunkContext?> LoadTrunkAsync(string? previousVersion, RunStats stats, CancellationToken ct)
    {
        AiContext context;
        try
        {
            context = await core.FetchContextAsync(ct);
        }
        catch (CoreAbortException ex)
        {
            WorkerLog.Error("core error, aborting run before context fetch: {Message}", ex.Message);
            return null;
        }

        if (string.IsNullOrEmpty(context.Resume) || context.Keywords == null || context.Inventory == null)
        {
            WorkerLog.Error("core error, aborting run: /ai/context payload is missing resume/keywords/inventory");
            return null;
        }

        var trunk = prompt.BuildTrunk(context);
        stats.RecordDroppedMemoryRows(trunk.DroppedMemoryRows);
        if (previousVersion != null && !string.Equals(context.ContextVersion, previousVersion, StringComparison.Ordinal))
            WorkerLog.Warn("context changed - trunk rebuilt (hash {Hash}); cache re-ingested once", trunk.Hash);
        return new TrunkContext(trunk, context.ContextVersion, context.AiPassmark);
    }

    private async Task<int> RunQueueAsync(RunStats stats, CancellationToken ct)
    {
        var current = await LoadTrunkAsync(null, stats, ct);
        if (current == null) return ExitCoreAbort;
        Banner(current.Trunk, current.Version);

        var number = 0;
        var consecutive_llm_failures = 0;
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

            if (next.JobId is not long job_id || next.Content is null || next.Fingerprint is null)
                return Abort(ExitCoreAbort, "core error, aborting run: /ai/next payload is missing jobId/content/fingerprint");

            if (!string.IsNullOrEmpty(next.ContextVersion) &&
                !string.Equals(next.ContextVersion, current.Version, StringComparison.Ordinal))
            {
                current = await LoadTrunkAsync(current.Version, stats, ct);
                if (current == null) return ExitCoreAbort;
            }

            number++;
            stats.Jobs++;
            WorkerLog.Info("job {JobId} #{Number}: content {Chars} chars, passmark {Passmark}",
                job_id, number, next.Content.Length, current.Passmark);

            var job_text = prompt.PrepareJob(current.Trunk, next);
            stats.RecordTruncation(job_id, job_text);

            try
            {
                VerdictPayload verdict;
                try
                {
                    verdict = await JudgeAsync(next, job_id, prompt.Compose(current.Trunk, next, job_text, current.Passmark), stats, ct);
                    consecutive_llm_failures = 0;
                }
                catch (LlmCallException ex)
                {
                    consecutive_llm_failures++;
                    stats.LlmFailures++;
                    if (consecutive_llm_failures >= MaxConsecutiveLlmFailures)
                        return Abort(ExitLlmUnavailable,
                            $"model failed on {consecutive_llm_failures} consecutive jobs, aborting run: {ex.Message}");
                    WorkerLog.Warn("job {JobId}: model call failed: {Message} - posting Error verdict and continuing",
                        job_id, ex.Message);
                    verdict = VerdictPayload.Error(job_id, next.Fingerprint!, $"model call failed: {ex.Message}");
                }

                if (Promotes(current.Passmark, verdict))
                {
                    stats.Promoted++;
                    try
                    {
                        verdict.Delta = await TailorAsync(job_id, prompt.ComposeTailor(current.Trunk, next, job_text), stats, ct);
                    }
                    catch (LlmCallException ex)
                    {
                        consecutive_llm_failures++;
                        stats.LlmFailures++;
                        if (consecutive_llm_failures >= MaxConsecutiveLlmFailures)
                            return Abort(ExitLlmUnavailable,
                                $"model failed on {consecutive_llm_failures} consecutive calls, aborting run: {ex.Message}");
                        WorkerLog.Warn("job {JobId}: tailoring call failed: {Message} - posting verdict without delta",
                            job_id, ex.Message);
                    }
                }

                if (verdict.Verdict == "Error")
                {
                    stats.ErrorVerdicts++;
                    stats.RecordErrorJob(job_id);
                }

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

    internal static bool Promotes(int passmark, VerdictPayload verdict)
    {
        if (verdict.Verdict == "Error") return false;
        return verdict.Relevance >= passmark;
    }

    private async Task<VerdictPayload> JudgeAsync(AiNextPayload next, long jobId,
        PromptBuilder.Prompt message, RunStats stats, CancellationToken ct)
    {
        LogPrompt(jobId, 1, message);
        Exception? last = null;

        for (var attempt = 1; attempt <= MaxModelAttempts; attempt++)
        {
            LlmResult? result = null;
            try
            {
                result = await llm.CompleteAsync(message.System, message.User,
                    VerdictSchema.ResponseFormat, attempt - 1, ct);
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

    private async Task<JsonNode?> TailorAsync(long jobId, PromptBuilder.Prompt message,
        RunStats stats, CancellationToken ct)
    {
        LogPrompt(jobId, 2, message);

        for (var attempt = 1; attempt <= MaxModelAttempts; attempt++)
        {
            LlmResult? result = null;
            try
            {
                result = await llm.CompleteAsync(message.System, message.User,
                    VerdictSchema.TailorResponseFormat, attempt - 1, ct);
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

    private void Banner(PromptBuilder.Trunk trunk, string? version)
    {
        WorkerLog.Info("run {RunId} starting: model {Model}, temperature {Temperature}, seed {Seed}",
            WorkerLog.RunId, options?.Model ?? "n/a", options?.Temperature ?? LlmOptions.DefaultTemperature,
            options?.Seed ?? 0);
        WorkerLog.Info("trunk {TrunkHash} (~{TrunkTokens} tok, version {Version}), rubric {RubricHash}, rubric-tailor {RubricTailorHash}",
            trunk.Hash, trunk.EstTokens, version ?? "n/a",
            options == null ? "n/a" : RunReport.ShortHash(options.Rubric),
            options == null ? "n/a" : RunReport.ShortHash(options.RubricTailor));
        WorkerLog.Info(
            "endpoints: core {Core}, llm {Llm}; context {MaxContextTokens} tok, memory cap {MemoryTokenCap} tok, slot {Slot}, timeout {TimeoutSeconds}s, max completion {MaxCompletionTokens} tok",
            options?.Core ?? "n/a", options?.BaseUrl ?? "n/a",
            PromptBuilder.MaxContextTokens, PromptBuilder.MemoryTokenCap,
            options?.Slot ?? LlmOptions.DefaultSlot,
            options?.TimeoutSeconds ?? LlmOptions.DefaultTimeoutSeconds,
            options?.MaxCompletionTokens ?? LlmOptions.DefaultMaxCompletionTokens);
    }

    private static void LogPrompt(long jobId, int callNo, PromptBuilder.Prompt message)
    {
        WorkerLog.Info(
            "job {JobId} call {Call} prompt: system {SystemChars} chars (~{SystemTokens} tok), user {UserChars} chars (~{UserTokens} tok)",
            jobId, callNo, message.System.Length, message.EstSystemTokens,
            message.User.Length, message.EstUserTokens);
        WorkerLog.Debug("job {JobId} call {Call} cacheable prefix ≈ {Prefix} tok",
            jobId, callNo, message.CacheablePrefixTokens);
        WorkerLog.Debug("job {JobId} call {Call} system prompt body:{NewLine}{System}",
            jobId, callNo, Environment.NewLine, message.System);
        WorkerLog.Debug("job {JobId} call {Call} user prompt body:{NewLine}{User}",
            jobId, callNo, Environment.NewLine, message.User);

        if (message.ContentTruncatedChars > 0)
            WorkerLog.Info("job {JobId} call {Call}: JD truncated {Chars} chars (~{Tokens} tok est)",
                jobId, callNo, message.ContentTruncatedChars,
                message.ContentTruncatedChars / PromptBuilder.CharsPerToken);
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

    private void Summary(string outcome, int code, RunStats stats)
    {
        WorkerLog.Info(
            "run {RunId} {Outcome} exit {Code}: jobs {Jobs}, promoted {Promoted}, errors {Errors}, 404 {Gone}, retries {Retries}, llm failures {LlmFailures}, wall {Wall:F1}s, avg {Avg:F1}s/job, tokens in {PromptTokens} / out {CompletionTokens}, length-finishes {FinishReasonLength}, truncated jobs {TruncatedJobs}, dropped memory rows {DroppedMemoryRows}",
            WorkerLog.RunId, outcome, code, stats.Jobs, stats.Promoted, stats.ErrorVerdicts, stats.Gone,
            stats.Retries, stats.LlmFailures, stats.WallSeconds, stats.AvgSecondsPerJob,
            stats.PromptTokens, stats.CompletionTokens, stats.FinishReasonLength,
            stats.TruncatedJobs, stats.DroppedMemoryRows);

        var report = RunReport.From(WorkerLog.RunId, code, options, stats);
        WorkerLog.WriteRunSummary(report.ToJson());
    }

    private static int Abort(int code, string message)
    {
        WorkerLog.Error("{Message}", message);
        return code;
    }
}
