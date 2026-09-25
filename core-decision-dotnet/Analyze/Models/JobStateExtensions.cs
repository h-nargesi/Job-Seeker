namespace Photon.JobSeeker;

public static class JobStateExtensions
{
    public static string CssClass(this JobState state) => state switch
    {
        JobState.Saved => "text-secondary",
        JobState.Revaluation => "text-secondary",
        JobState.NotApprovedRegex => "text-warning",
        JobState.AiPending => "text-primary",
        JobState.NotApprovedAI => "text-warning",
        JobState.AIError => "text-danger",
        JobState.Attention => "text-info",
        JobState.Rejected => "text-danger",
        JobState.Applied => "text-success",
        _ => "text-secondary",
    };
}
