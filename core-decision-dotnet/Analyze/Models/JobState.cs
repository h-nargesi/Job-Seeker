namespace Photon.JobSeeker;

public enum JobState
{
    Saved = 1,
    Revaluation,
    NotApprovedRegex,
    AiPending,
    NotApprovedAI,
    AIError,
    Attention,
    Rejected,
    Applied,
}
