namespace Photon.JobSeeker;

public static class JobRanking
{
    public const double FreshPenalty = 0.85;
    public const double SweetStart = 4;
    public const double SweetEnd = 10;
    public const double TwoWeekWeight = 0.75;
    public const double OldWeight = 0.25;
    public const double Floor = 0.15;

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

    public static double Effective(long score, double ageDays) => score * Weight(ageDays);
}
