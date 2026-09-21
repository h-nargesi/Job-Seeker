namespace Photon.JobSeeker
{
    partial class JobBusiness
    {
        public Job? FetchNextAiPending()
        {
            return database.Query<Job>(Q_FETCH_NEXT_AI).FirstOrDefault();
        }

        public List<Job> FetchAttentionJobs(int limit = 100)
        {
            return database.Query<Job>($@"
SELECT * FROM Job WHERE State = '{nameof(JobState.Attention)}'
ORDER BY ModifiedOn DESC
LIMIT {limit}").ToList();
        }

        public bool ApplyAiVerdict(long jobId, AiVerdictUpdate update, ResumeInventory? inventory = null)
        {
            var job = Fetch(jobId);
            if (job == null) return false;

            job.AiScore = update.AiScore;
            job.AiVerdict = update.AiVerdict;
            job.AiReason = update.AiReason;
            job.AiSeniority = update.AiSeniority;
            job.AiSalaryMin = update.AiSalaryMin;
            job.AiSalaryMax = update.AiSalaryMax;
            job.AiCurrency = update.AiCurrency;
            job.AiPeriod = update.AiPeriod;
            job.AiWorkModel = update.AiWorkModel;
            job.AiRelocation = update.AiRelocation;
            job.AiContract = update.AiContract;
            job.AiExperienceYears = update.AiExperienceYears;
            job.AiSkills = update.AiSkills;

            var mismatch = job.Content == null
                || !string.Equals(
                    JobContent.Fingerprint(job.Content),
                    update.Fingerprint,
                    StringComparison.OrdinalIgnoreCase);

            var queued = job.State == JobState.AiPending;
            var promoting = false;
            if (!mismatch && queued)
            {
                if (update.AiVerdict == AiVerdict.Error)
                    job.State = JobState.AIError;
                else if (update.AiScore >= database.AppSetting.AiPassmark())
                {
                    job.State = JobState.Attention;
                    promoting = true;
                }
                else
                    job.State = JobState.NotApprovedAI;
            }

            AiTailoring.Apply(job, update, promoting && !mismatch, inventory);

            var note = mismatch
                ? "**AI verdict** (fingerprint mismatch — no state change)"
                : queued
                    ? "**AI verdict**"
                    : "**AI verdict** (informational — not in queue)";
            if (!string.IsNullOrEmpty(update.AiReason))
                note += "\n" + update.AiReason;
            if (!string.IsNullOrEmpty(update.TailoringNote))
                note += "\n" + update.TailoringNote;

            job.Log = AppendLog(job.Log, note);
            PersistVerdict(job);
            return true;
        }

        public bool RequeueJob(long jobId)
        {
            database.Execute(Q_REQUEUE, new { now = DateTime.Now, jobId });
            return database.Changes() == 1;
        }

        public bool PromoteJob(long jobId)
        {
            var job = Fetch(jobId);
            if (job == null) return false;
            if (job.State is not (JobState.AiPending or JobState.NotApprovedAI or JobState.AIError))
                return false;

            job.State = JobState.Attention;
            job.Log = AppendLog(job.Log, $"Manually promoted (emergency) — {DateTime.Now:yyyy-MM-dd}");
            database.Execute(Q_PROMOTE, new
            {
                state = job.State.ToString(),
                log = job.Log,
                now = DateTime.Now,
                jobId,
            });
            return true;
        }

        private void PersistVerdict(Job job)
        {
            database.Execute(Q_APPLY_VERDICT, new
            {
                aiScore = job.AiScore,
                aiVerdict = job.AiVerdict?.ToString(),
                aiReason = job.AiReason,
                aiSeniority = job.AiSeniority?.ToString(),
                aiSalaryMin = job.AiSalaryMin,
                aiSalaryMax = job.AiSalaryMax,
                aiCurrency = job.AiCurrency,
                aiPeriod = job.AiPeriod?.ToString(),
                aiWorkModel = job.AiWorkModel?.ToString(),
                aiRelocation = job.AiRelocation?.ToString(),
                aiContract = job.AiContract?.ToString(),
                aiExperienceYears = job.AiExperienceYears,
                aiSkills = job.AiSkills,
                aiOptions = job.AiOptions,
                resumeText = job.ResumeText,
                log = job.Log,
                state = job.State.ToString(),
                now = DateTime.Now,
                jobId = job.JobID,
            });
        }

        private static string AppendLog(string? existing, string line)
        {
            return string.IsNullOrEmpty(existing) ? line : existing + "\n" + line;
        }

        private readonly static string Q_FETCH_NEXT_AI = $@"
SELECT * FROM Job
WHERE State = '{nameof(JobState.AiPending)}' AND Content IS NOT NULL
ORDER BY JobID
LIMIT 1";

        private readonly static string Q_REQUEUE = $@"
UPDATE Job SET State = '{nameof(JobState.AiPending)}', ModifiedOn = @now
WHERE JobID = @jobId AND Content IS NOT NULL AND State IN (
    '{nameof(JobState.AIError)}',
    '{nameof(JobState.NotApprovedAI)}')";

        private readonly static string Q_PROMOTE = @"
UPDATE Job SET State = @state, Log = @log, ModifiedOn = @now
WHERE JobID = @jobId";

        private readonly static string Q_APPLY_VERDICT = @"
UPDATE Job SET
    AiScore = @aiScore,
    AiVerdict = @aiVerdict,
    AiReason = @aiReason,
    AiSeniority = @aiSeniority,
    AiSalaryMin = @aiSalaryMin,
    AiSalaryMax = @aiSalaryMax,
    AiWorkModel = @aiWorkModel,
    AiRelocation = @aiRelocation,
    AiContract = @aiContract,
    AiPeriod = @aiPeriod,
    AiCurrency = @aiCurrency,
    AiExperienceYears = @aiExperienceYears,
    AiSkills = @aiSkills,
    AiOptions = @aiOptions,
    ResumeText = @resumeText,
    Log = @log,
    State = @state,
    ModifiedOn = @now
WHERE JobID = @jobId";
    }
}
