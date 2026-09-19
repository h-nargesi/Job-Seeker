using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Photon.JobSeeker;

public sealed partial class ResumeInventoryCache(IViewRenderService views)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private ResumeInventory? inventory;

    public async Task<ResumeInventory> GetAsync(HttpContext http)
    {
        if (inventory != null) return inventory;

        await gate.WaitAsync();
        try
        {
            if (inventory != null) return inventory;

            var html = await views.RenderToStringAsync(http, "~/views/resume.cshtml",
                new ResumePage { Context = ResumeHtml.MasterContext() });

            inventory = ResumeInventoryParser.Parse(html);
            return inventory;
        }
        finally
        {
            gate.Release();
        }
    }
}

public static partial class ResumeInventoryParser
{
    [GeneratedRegex(@"^key-[\w-]+$")]
    private static partial Regex KeyClass();

    public static ResumeInventory Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var items = new List<ResumeInventoryItem>();
        var selectors = new HashSet<string>(StringComparer.Ordinal);

        AddSlot(items, ResumeInventory.TitleSlot, doc.DocumentNode.SelectSingleNode("//h1[contains(@class,'header-job-title')]"));
        AddSlot(items, ResumeInventory.SummarySlot, doc.DocumentNode.SelectSingleNode("//p[@id='summary']"));

        foreach (var article in SelectNodes(doc, "//article[@id]"))
        {
            var id = "#" + article.Id;
            if (!selectors.Add(id)) continue;

            items.Add(new ResumeInventoryItem(
                id,
                ResumeInventoryItem.BlockType,
                KeyClasses(article),
                Excerpt(article.InnerText)));

            foreach (var bullet in Bullets(article))
            {
                var selector = $"{id} li:nth-child({bullet.index})";
                if (selectors.Add(selector))
                    items.Add(new ResumeInventoryItem(
                        selector,
                        ResumeInventoryItem.BulletType,
                        [],
                        bullet.text.Trim()));
            }
        }

        foreach (var key in KeySelectors(doc))
            if (selectors.Add(key.selector))
                items.Add(new ResumeInventoryItem(
                    key.selector,
                    ResumeInventoryItem.KeyType,
                    [key.name],
                    key.excerpt));

        return new ResumeInventory(items);
    }

    private static IEnumerable<HtmlNode> SelectNodes(HtmlNode scope, string xpath)
    {
        var nodes = scope.SelectNodes(xpath);
        return nodes == null ? [] : nodes.Cast<HtmlNode>();
    }

    private static IEnumerable<HtmlNode> SelectNodes(HtmlDocument document, string xpath)
        => SelectNodes(document.DocumentNode, xpath);

    private static void AddSlot(List<ResumeInventoryItem> items, string slot, HtmlNode? node)
    {
        if (node == null) return;
        items.Add(new ResumeInventoryItem(slot, ResumeInventoryItem.SlotType, [], node.InnerText.Trim()));
    }

    private static IEnumerable<(int index, string text)> Bullets(HtmlNode article)
    {
        foreach (var list in SelectNodes(article, ".//ul"))
        {
            var index = 0;
            foreach (var child in list.ChildNodes)
            {
                if (child.Name != "li") continue;
                index++;
                yield return (index, child.InnerText);
            }
        }
    }

    private static IEnumerable<(string selector, string name, string excerpt)> KeySelectors(HtmlDocument doc)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in SelectNodes(doc, "//*[@class]"))
        {
            foreach (var class_name in node.GetClasses())
            {
                if (!KeyClass().IsMatch(class_name) || !seen.Add(class_name)) continue;
                yield return ($".{class_name}", class_name, Excerpt(node.InnerText));
            }
        }
    }

    private static IReadOnlyList<string> KeyClasses(HtmlNode node)
    {
        return node.GetClasses().Where(c => KeyClass().IsMatch(c)).ToList();
    }

    private static string Excerpt(string text)
    {
        var trimmed = Regex.Replace(text, @"\s+", " ").Trim();
        return trimmed.Length <= ResumeInventory.ExcerptLength ? trimmed : trimmed[..ResumeInventory.ExcerptLength];
    }
}
