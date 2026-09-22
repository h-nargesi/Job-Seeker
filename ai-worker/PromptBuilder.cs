using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiWorker;

public sealed class PromptBuilder(string rubricTemplate, string rubricTailorTemplate,
    int completionReserveTokens = 2048)
{
    public const string KeywordsPlaceholder = "{{keywords}}";
    public const int MaxContextTokens = 16000;
    public const int CharsPerToken = 4;
    public const int MemoryTokenCap = 1500;
    public const int DefaultCompletionReserveTokens = 2048;
    public const int EstimateSlackTokens = 512;
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

    public sealed record Prompt(
        string System,
        string User,
        int EstSystemTokens,
        int EstContentTokens,
        int ContentOriginalChars,
        int ContentTruncatedChars,
        int DroppedMemoryRows,
        int TotalMemoryRows);

    public Prompt Compose(AiNextPayload next, IReadOnlyList<MemorySnapshotRow>? rankingMemory = null)
    {
        var rubric = rubricTemplate.Replace(KeywordsPlaceholder, KeywordsJson(next.Keywords), StringComparison.Ordinal);
        var memory = MemoryBlock(rankingMemory, out var dropped, out var total);
        var system = string.Join("\n\n",
            rubric,
            $"{RankingMemoryLabel}\n{memory}");
        if (!string.IsNullOrEmpty(next.Resume))
            system += $"\n\n{ResumeLabel}\n{next.Resume}";

        var budget = ContentBudgetTokens(system);
        return BuildPrompt(system, next.Content, budget, dropped, total);
    }

    public Prompt ComposeTailor(AiNextPayload next, IReadOnlyList<MemorySnapshotRow>? resumeMemory = null)
    {
        var rubric = rubricTailorTemplate.Replace(KeywordsPlaceholder, KeywordsJson(next.Keywords), StringComparison.Ordinal);
        var memory = MemoryBlock(resumeMemory, out var dropped, out var total);
        var system = string.Join("\n\n",
            rubric,
            $"{ResumeMemoryLabel}\n{memory}",
            $"{InventoryLabel}\n{InventoryJson(next.Inventory)}",
            $"{SelectionLabel}\n{(string.IsNullOrEmpty(next.Options) ? "{}" : next.Options)}");

        var budget = ContentBudgetTokens(system);
        return BuildPrompt(system, next.Content, budget, dropped, total);
    }

    private int ContentBudgetTokens(string system)
    {
        return MaxContextTokens - completionReserveTokens - EstimateSlackTokens - Estimate(system);
    }

    private static Prompt BuildPrompt(string system, string? content, int budgetTokens, int droppedRows, int totalRows)
    {
        var original = content ?? string.Empty;
        var truncated = TailTruncate(original, budgetTokens);
        return new Prompt(system, $"{JobLabel}\n{truncated}",
            Estimate(system), Estimate(truncated),
            original.Length, original.Length - truncated.Length,
            droppedRows, totalRows);
    }

    internal static string MemoryBlock(IReadOnlyList<MemorySnapshotRow>? rows, out int droppedRows, out int totalRows)
    {
        totalRows = rows?.Count ?? 0;
        if (rows == null || rows.Count == 0)
        {
            droppedRows = 0;
            return NoMemoryText;
        }

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

        droppedRows = rows.Count - lines.Count;
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
