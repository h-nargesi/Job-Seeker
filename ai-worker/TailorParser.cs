using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiWorker;

public static class TailorParser
{
    public static JsonNode? Parse(string content)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new ModelOutputException($"invalid tailoring JSON: {ex.Message}");
        }

        if (node is not JsonObject root)
            throw new ModelOutputException("tailoring delta root must be a JSON object");

        ValidateStrings(root, "keys");
        ValidateStrings(root, "included");
        ValidateStrings(root, "notIncluded");

        if (root.TryGetPropertyValue("length", out var length) && length is not null)
        {
            if (length.GetValueKind() != JsonValueKind.Number || (int?)length is not 1 and not 2)
                throw new ModelOutputException("tailoring length must be 1 or 2");
        }

        if (root.TryGetPropertyValue("texts", out var texts) && texts is not null)
        {
            if (texts.AsObject().Any(entry => entry.Value is not JsonValue value || !value.TryGetValue<string>(out _)))
                throw new ModelOutputException("tailoring texts values must be strings");
        }

        return node;
    }

    private static void ValidateStrings(JsonObject root, string name)
    {
        if (!root.TryGetPropertyValue(name, out var value) || value is null) return;

        if (value.GetValueKind() != JsonValueKind.Array)
            throw new ModelOutputException($"tailoring {name} must be an array of strings");

        foreach (var item in value.AsArray())
            if (item is not JsonValue entry || !entry.TryGetValue<string>(out _))
                throw new ModelOutputException($"tailoring {name} must be an array of strings");
    }
}
