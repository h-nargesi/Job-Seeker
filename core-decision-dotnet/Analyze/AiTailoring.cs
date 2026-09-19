using System.Text.Json;

namespace Photon.JobSeeker;

public static class AiTailoring
{
    public const int KeysCap = 8;
    public const int SelectorsCap = 30;
    public const int TitleMaxLength = 80;
    public const int DeltaLogMaxLength = 4000;

    public static void Apply(Job job, AiVerdictUpdate update, bool promoting, ResumeInventory? inventory)
    {
        if (update.Delta is not JsonElement delta) return;

        if (!promoting)
        {
            update.TailoringNote = Note(delta, "dropped — verdict does not promote");
            return;
        }

        if (delta.ValueKind != JsonValueKind.Object)
        {
            update.TailoringNote = Note(delta, "dropped — delta is not a JSON object");
            return;
        }

        var selection = Selection(job, delta, inventory, out var selection_error);
        var texts = Texts(job, delta, inventory, out var texts_error);

        var notes = new List<string>();

        if (selection != null)
        {
            job.AiOptions = selection;
            notes.Add("selection applied to AiOptions");
        }
        else notes.Add($"selection dropped — {selection_error}");

        if (texts != null)
        {
            job.ResumeText = texts;
            notes.Add($"texts proposed for {texts.Count} slot(s)");
        }
        else notes.Add($"texts dropped — {texts_error}");

        update.TailoringNote = Note(delta, string.Join("; ", notes));
    }

    private static ResumeContext? Selection(Job job, JsonElement delta, ResumeInventory? inventory, out string error)
    {
        error = string.Empty;

        List<string>? keys = null;
        List<string>? included = null;
        List<string>? not_included = null;

        if (!ReadStrings(delta, "keys", ref keys, out error) ||
            !ReadStrings(delta, "included", ref included, out error) ||
            !ReadStrings(delta, "notIncluded", ref not_included, out error))
            return null;

        var length = 0;
        var has_length = false;
        if (delta.TryGetProperty("length", out var length_value) && length_value.ValueKind != JsonValueKind.Null)
        {
            if (length_value.ValueKind != JsonValueKind.Number || !length_value.TryGetInt32(out length) ||
                length is < 1 or > 2)
            {
                error = "length must be 1 or 2";
                return null;
            }
            has_length = true;
        }

        if (keys == null && included == null && not_included == null && !has_length)
        {
            error = "no selection levers present";
            return null;
        }

        if (keys != null)
        {
            if (keys.Count > KeysCap)
            {
                error = $"more than {KeysCap} keys";
                return null;
            }
            if (keys.Any(key => !ResumeContext.KeysContext.MainKeys.Contains(key)))
            {
                error = "keys outside MainKeys";
                return null;
            }
        }

        var selector_count = (included?.Count ?? 0) + (not_included?.Count ?? 0);
        if (selector_count > SelectorsCap)
        {
            error = $"more than {SelectorsCap} selectors";
            return null;
        }

        if (inventory != null)
        {
            if (included != null && included.Any(selector => !inventory.SelectionSelectors.Contains(selector)))
            {
                error = "included selector outside inventory";
                return null;
            }
            if (not_included != null && not_included.Any(selector => !inventory.SelectionSelectors.Contains(selector)))
            {
                error = "notIncluded selector outside inventory";
                return null;
            }
        }

        var context = (job.Options ?? new ResumeContext()).Clone();

        if (keys != null)
        {
            foreach (var key in ResumeContext.KeysContext.MainKeys)
                context.Keys[key] = keys.Contains(key)
                    ? context.Keys[key] ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    : null;
        }

        if (not_included != null)
        {
            context.NotIncluded.Clear();
            foreach (var selector in not_included) context.NotIncluded.Add(selector);
        }

        if (included != null)
        {
            context.Included.Clear();
            foreach (var selector in included) context.Included.Add(selector);
        }

        if (has_length) context.Length = length;

        return context;
    }

    private static ResumeText? Texts(Job job, JsonElement delta, ResumeInventory? inventory, out string error)
    {
        error = string.Empty;

        if (!delta.TryGetProperty("texts", out var texts_value) || texts_value.ValueKind == JsonValueKind.Null)
            return null;

        if (texts_value.ValueKind != JsonValueKind.Object)
        {
            error = "texts must be an object";
            return null;
        }

        var slots = new Dictionary<string, string>();
        foreach (var entry in texts_value.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.String)
            {
                error = $"texts[{entry.Name}] must be a string";
                return null;
            }

            slots[entry.Name] = entry.Value.GetString() ?? string.Empty;
        }

        if (slots.Count == 0)
        {
            error = "texts object is empty";
            return null;
        }

        foreach (var (slot, text) in slots)
        {
            if (inventory != null && !inventory.TextSlots.Contains(slot))
            {
                error = $"texts slot '{slot}' is not a known text slot";
                return null;
            }

            if (text.Contains('<') || text.Contains('>'))
            {
                error = $"texts slot '{slot}' contains HTML";
                return null;
            }

            if (slot == ResumeInventory.TitleSlot &&
                (text.Contains('\n') || text.Contains('\r') || text.Length > TitleMaxLength))
            {
                error = $"texts slot '{ResumeInventory.TitleSlot}' must be one line of at most {TitleMaxLength} characters";
                return null;
            }
        }

        return Merge(job.ResumeText, slots);
    }

    internal static ResumeText Merge(ResumeText? current, Dictionary<string, string> slots)
    {
        var text = new ResumeText();

        foreach (var (slot, value) in current ?? [])
        {
            text[slot] = new ResumeTextSlot
            {
                Live = value.Live,
                Proposal = value.Proposal,
                Status = value.Status,
            };
        }

        foreach (var (slot, proposal) in slots)
        {
            var existing = text.TryGetValue(slot, out var slot_value) ? slot_value : null;

            if (existing != null && existing.Status == ResumeTextSlotStatus.Rejected &&
                existing.Proposal == proposal)
                continue;

            text[slot] = new ResumeTextSlot
            {
                Live = existing?.Live,
                Proposal = proposal,
                Status = ResumeTextSlotStatus.Pending,
            };
        }

        return text;
    }

    private static bool ReadStrings(JsonElement delta, string name, ref List<string>? target, out string error)
    {
        error = string.Empty;
        if (!delta.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return true;

        if (value.ValueKind != JsonValueKind.Array)
        {
            error = $"{name} must be an array of strings";
            return false;
        }

        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                error = $"{name} must be an array of strings";
                return false;
            }
            list.Add(item.GetString() ?? string.Empty);
        }

        target = list;
        return true;
    }

    private static string Note(JsonElement delta, string verdict)
    {
        var raw = delta.GetRawText();
        if (raw.Length > DeltaLogMaxLength) raw = raw[..DeltaLogMaxLength] + "…";
        return $"**AI delta** ({verdict})\n{raw}";
    }
}
