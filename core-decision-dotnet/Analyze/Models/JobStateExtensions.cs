namespace Photon.JobSeeker;

public static class JobStateExtensions
{
    public static string CssClass(this JobState state) => state switch
    {
        JobState.Saved => "job-state-saved",
        JobState.Revaluation => "job-state-revaluation",
        JobState.NotApprovedRegex => "job-state-not-approved-regex",
        JobState.AiPending => "job-state-ai-pending",
        JobState.NotApprovedAI => "job-state-not-approved-ai",
        JobState.AIError => "job-state-ai-error",
        JobState.Attention => "job-state-attention",
        JobState.Rejected => "job-state-rejected",
        JobState.Applied => "job-state-applied",
        JobState.Failed => "job-state-failed",
        _ => "job-state-saved",
    };
}
