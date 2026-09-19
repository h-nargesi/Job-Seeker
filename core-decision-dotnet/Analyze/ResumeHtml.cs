using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Photon.JobSeeker;

public static partial class ResumeHtml
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

    public static ResumeContext Selection(Job job)
    {
        if (job.Options?.HumanEdited == true) return job.Options;
        return job.AiOptions ?? job.Options ?? new ResumeContext();
    }

    public static Dictionary<string, string> LiveText(Job job)
    {
        var result = new Dictionary<string, string>();
        foreach (var (slot, value) in job.ResumeText ?? [])
            if (!string.IsNullOrEmpty(value.Live))
                result[slot] = value.Live;
        return result;
    }

    public static bool HasPendingProposal(Job job)
    {
        foreach (var (_, slot) in job.ResumeText ?? [])
            if (slot.Status == ResumeTextSlotStatus.Pending && !string.IsNullOrEmpty(slot.Proposal))
                return true;
        return false;
    }

    public static string RenderText(string html, Dictionary<string, string> liveText)
    {
        return CapFromTop(JobContent.GetTextContent(ApplyLiveText(html, liveText)));
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

    public static string ApplyLiveText(string html, Dictionary<string, string> liveText)
    {
        if (liveText.Count == 0) return html;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var (slot, value) in liveText)
        {
            var node = FindSlotNode(doc, slot);
            if (node == null) continue;

            node.ChildNodes.Clear();
            node.AppendChild(doc.CreateTextNode(value));
        }

        return doc.DocumentNode.OuterHtml;
    }

    private static HtmlNode? FindSlotNode(HtmlDocument doc, string slot)
    {
        var xpath = slot switch
        {
            ResumeInventory.TitleSlot => "//h1[contains(@class,'header-job-title')]",
            ResumeInventory.SummarySlot => "//p[@id='summary']",
            _ => SlotXPath(slot),
        };

        return xpath == null ? null : doc.DocumentNode.SelectSingleNode(xpath);
    }

    private static string? SlotXPath(string slot)
    {
        var match = SlotSelector().Match(slot);
        if (!match.Success) return null;

        var id = match.Groups["id"].Value;
        return match.Groups["n"].Success
            ? $"//*[@id='{id}']//li[{match.Groups["n"].Value}]"
            : $"//*[@id='{id}']";
    }

    [GeneratedRegex(@"^#(?<id>[\w-]+)(?:\s+li:nth-child\((?<n>\d+)\))?$")]
    private static partial Regex SlotSelector();
}
