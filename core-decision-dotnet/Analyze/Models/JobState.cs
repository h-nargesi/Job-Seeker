namespace Photon.JobSeeker;

public enum JobState
{
    Saved = 1,
    Failed,
    Revaluation,
    NotApprovedRegex,
    AiPending,
    AIError,
    NotApprovedAI,
    Attention,
    Rejected,
    Applied,
}
