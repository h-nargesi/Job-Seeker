using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Photon.JobSeeker;

public static class JobContent
{
    private static readonly Regex consecutive_whitespace = new(@"\s+");
    private static readonly Regex remove_new_lines = new(@"(?<=\n)[\n\s]+");
    private static readonly HashSet<string> invalid_tag =
    [
        "script", "head", "style"
    ];

    public static string GetTextContent(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var root = doc.DocumentNode;
        var buffer = new StringBuilder();
        foreach (var node in root.DescendantsAndSelf())
        {
            if (node.HasChildNodes) continue;
            if (invalid_tag.Contains(node.ParentNode.Name)) continue;

            string text = node.InnerText;
            if (!string.IsNullOrEmpty(text))
                buffer.Append(' ').Append(text.Trim());
        }

        return remove_new_lines.Replace(buffer.ToString(), "\n");
    }

    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return consecutive_whitespace.Replace(text, " ").Trim();
    }

    public static bool HasChanged(string? stored, string? incoming)
    {
        return Normalize(stored) != Normalize(incoming);
    }
}
