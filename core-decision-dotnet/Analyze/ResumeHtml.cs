namespace Photon.JobSeeker;

public static class ResumeHtml
{
    public const int TokenCap = 16_000;
    public const int CharsPerToken = 4;

    public static ResumeContext MasterContext()
    {
        var context = new ResumeContext();
        foreach (var key in ResumeContext.KeysContext.MainKeys)
            context.Keys[key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return context;
    }

    public static string PruneStripCap(string html)
    {
        return CapFromTop(JobContent.GetTextContent(html));
    }

    public static string CapFromTop(string text, int tokenCap = TokenCap)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var max_chars = tokenCap * CharsPerToken;
        return text.Length <= max_chars ? text : text[..max_chars];
    }
}
