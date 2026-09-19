using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiWorker;

public sealed class PromptBuilder(string rubricTemplate, string rubricTailorTemplate)
{
    public const string KeywordsPlaceholder = "{{keywords}}";
    public const int MaxContextTokens = 16000;
    public const int CharsPerToken = 4;
    public const int MemoryTokenCap = 1500;
    public const string NoMemoryText = "(none confirmed yet)";

    public const string RankingMemoryLabel = "## RANKING MEMORY";
    public const string ResumeMemoryLabel = "## RESUME MEMORY";
    public const string InventoryLabel = "## BLOCK INVENTORY";
    public const string SelectionLabel = "## CURRENT SELECTION";
    public const string ResumeLabel = "## CANDIDATE RESUME";
    public const string JobLabel = "## JOB POSTING";

    private static readonly JsonSerializerOptions KeywordJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly JsonSerializerOptions MemoryJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public sealed record Prompt(string System, string User);

    public Prompt Compose(AiNextPayload next, IReadOnlyList<MemorySnapshotRow>? rankingMemory = null)
    {
        var rubric = rubricTemplate.Replace(KeywordsPlaceholder, KeywordsJson(next.Keywords), StringComparison.Ordinal);
        var system = string.Join("\n\n",
            rubric,
            $"{RankingMemoryLabel}\n{MemoryBlock(rankingMemory)}");
        if (!string.IsNullOrEmpty(next.Resume))
            system += $"\n\n{ResumeLabel}\n{next.Resume}";

        var budget = MaxContextTokens - Estimate(system);
        var content = TailTruncate(next.Content ?? string.Empty, budget);
        return new Prompt(system, $"{JobLabel}\n{content}");
    }

    public Prompt ComposeTailor(AiNextPayload next, IReadOnlyList<MemorySnapshotRow>? resumeMemory = null)
    {
        var rubric = rubricTailorTemplate.Replace(KeywordsPlaceholder, KeywordsJson(next.Keywords), StringComparison.Ordinal);
        var system = string.Join("\n\n",
            rubric,
            $"{ResumeMemoryLabel}\n{MemoryBlock(resumeMemory)}",
            $"{InventoryLabel}\n{InventoryJson(next.Inventory)}",
            $"{SelectionLabel}\n{(string.IsNullOrEmpty(next.Options) ? "{}" : next.Options)}");

        var budget = MaxContextTokens - Estimate(system);
        var content = TailTruncate(next.Content ?? string.Empty, budget);
        return new Prompt(system, $"{JobLabel}\n{content}");
    }

    internal static string MemoryBlock(IReadOnlyList<MemorySnapshotRow>? rows)
    {
        if (rows == null || rows.Count == 0) return NoMemoryText;

        var budget = MemoryTokenCap * CharsPerToken;
        var lines = new List<string>(rows.Count);
        var used = 0;

        foreach (var row in rows)
        {
            var line = JsonSerializer.Serialize(row, MemoryJson);
            if (used + line.Length > budget) break;
            lines.Add(line);
            used += line.Length;
        }

        return lines.Count == 0 ? NoMemoryText : string.Join("\n", lines);
    }

    internal static string InventoryJson(IReadOnlyList<InventoryItem>? inventory)
    {
        return JsonSerializer.Serialize(inventory ?? (IReadOnlyList<InventoryItem>)Array.Empty<InventoryItem>(), KeywordJson);
    }

    internal static string KeywordsJson(IReadOnlyList<JobKeyword>? keywords)
    {
        return JsonSerializer.Serialize(keywords ?? (IReadOnlyList<JobKeyword>)Array.Empty<JobKeyword>(), KeywordJson);
    }

    internal static int Estimate(string text)
    {
        return text.Length / CharsPerToken;
    }

    internal static string TailTruncate(string text, int maxTokens)
    {
        var maxChars = Math.Max(maxTokens, 0) * CharsPerToken;
        return text.Length <= maxChars ? text : text[..maxChars];
    }
}
