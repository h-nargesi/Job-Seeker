namespace Photon.JobSeeker;

public sealed record ResumeInventoryItem(string Id, string Type, IReadOnlyList<string> Keys, string Text)
{
    public const string SlotType = "slot";
    public const string BlockType = "block";
    public const string BulletType = "bullet";
    public const string KeyType = "key";
}

public sealed class ResumeInventory
{
    public const string TitleSlot = "title";
    public const string SummarySlot = "summary";
    public const int ExcerptLength = 120;

    public ResumeInventory(IReadOnlyList<ResumeInventoryItem> items)
    {
        Items = items;

        var selectors = new HashSet<string>(StringComparer.Ordinal);
        var text_slots = new HashSet<string>(StringComparer.Ordinal) { TitleSlot, SummarySlot };

        foreach (var item in items)
        {
            selectors.Add(item.Id);
            if (item.Type is ResumeInventoryItem.SlotType or ResumeInventoryItem.BulletType)
                text_slots.Add(item.Id);
        }

        SelectionSelectors = selectors;
        TextSlots = text_slots;
    }

    public IReadOnlyList<ResumeInventoryItem> Items { get; }

    public IReadOnlySet<string> SelectionSelectors { get; }

    public IReadOnlySet<string> TextSlots { get; }
}
