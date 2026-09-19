namespace Photon.JobSeeker;

public sealed class JobKeyword
{
    public required string Category { get; init; }

    public required long Score { get; init; }

    public required string Title { get; init; }
}

internal static class JobKeywords
{
    public const string RejectCategory = "reject";

    public static List<JobKeyword> From(IEnumerable<JobOption> options)
    {
        return options
            .Where(option => !string.Equals(option.Category, RejectCategory, StringComparison.OrdinalIgnoreCase))
            .Select(option => new JobKeyword
            {
                Category = option.Category,
                Score = option.Score,
                Title = option.Title,
            })
            .ToList();
    }
}
