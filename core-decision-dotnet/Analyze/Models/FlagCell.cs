namespace Photon.JobSeeker;

public sealed record FlagCell(string Text, string CssClass)
{
    public static FlagCell Create(bool? aiValue, int regexFlag)
    {
        if (aiValue is bool ai)
        {
            var agree = (regexFlag == 1) == ai;
            return new FlagCell(ai ? "true" : "false",
                agree ? (ai ? "text-success fw-bold" : "fw-bold") : "text-warning");
        }

        if (regexFlag < 0) return new FlagCell("—", "");
        return new FlagCell(regexFlag == 1 ? "true" : "false", "");
    }

    public static FlagCell ForRelocation(AiRelocation? value, int regexFlag)
    {
        return Create(value switch
        {
            AiRelocation.Yes => true,
            AiRelocation.No => false,
            _ => (bool?)null,
        }, regexFlag);
    }

    public static FlagCell ForRemote(AiWorkModel? value, bool includeHybrid, int regexFlag)
    {
        return Create(value switch
        {
            AiWorkModel.Remote => true,
            AiWorkModel.Hybrid => includeHybrid,
            AiWorkModel.Onsite => false,
            _ => (bool?)null,
        }, regexFlag);
    }
}
