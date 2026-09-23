namespace Photon.JobSeeker;

public static class JobRanking
{
    public const double FreshPenalty = 0.85;
    public const double SweetStart = 4;
    public const double SweetEnd = 10;
    public const double TwoWeekWeight = 0.75;
    public const double OldWeight = 0.25;
    public const double Floor = 0.15;

    public const string SqlRegexNorm =
        "(CASE WHEN Score IS NULL THEN 0 ELSE MIN(Score, @scoreCap) * 100.0 / @scoreCap END)";

    public const string SqlFinalScore =
        $"(@wRegex * {SqlRegexNorm} + @wAi * AiScore)";

    public const string SqlRankScore = $@"
CASE
WHEN State IN ('{nameof(JobState.Attention)}', '{nameof(JobState.NotApprovedAI)}') AND AiScore IS NOT NULL
THEN {SqlFinalScore}
ELSE {SqlRegexNorm}
END";

    public const string SqlAgeWeight = @"
CASE
WHEN AgeDays <= 2  THEN 0.85
WHEN AgeDays <= 4  THEN 0.85 + 0.15 * (AgeDays - 2) / 2
WHEN AgeDays <= 10 THEN 1.0
WHEN AgeDays <= 14 THEN 1.0 - 0.25 * (AgeDays - 10) / 4
WHEN AgeDays <= 28 THEN 0.75 - 0.50 * (AgeDays - 14) / 14
ELSE 0.15
END";

    public const string SqlEffectiveScore = $"{SqlRankScore} * {SqlAgeWeight}";

    public static double Weight(double ageDays)
    {
        if (ageDays <= 2) return FreshPenalty;
        if (ageDays <= SweetStart)
            return FreshPenalty + (1.0 - FreshPenalty) * (ageDays - 2) / (SweetStart - 2);
        if (ageDays <= SweetEnd) return 1.0;
        if (ageDays <= 14)
            return 1.0 - (1.0 - TwoWeekWeight) * (ageDays - SweetEnd) / 4;
        if (ageDays <= 28)
            return TwoWeekWeight - (TwoWeekWeight - OldWeight) * (ageDays - 14) / 14;
        return Floor;
    }

    public static double RegexNorm(long? score, int scoreCap)
    {
        if (scoreCap <= 0) return 0;
        var capped = Math.Min(score ?? 0, scoreCap);
        return capped * 100.0 / scoreCap;
    }

    public static double FinalScore(long? score, int aiScore, int scoreCap, double wRegex, double wAi)
        => wRegex * RegexNorm(score, scoreCap) + wAi * aiScore;

    public static double RankScore(
        JobState state, long? score, int? aiScore, int scoreCap, double wRegex, double wAi)
    {
        if (aiScore is int ai && state is JobState.Attention or JobState.NotApprovedAI)
            return FinalScore(score, ai, scoreCap, wRegex, wAi);
        return RegexNorm(score, scoreCap);
    }

    public static double Effective(double rankScore, double ageDays) => rankScore * Weight(ageDays);
}
