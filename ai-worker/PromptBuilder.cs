using System.Text.Json;

namespace AiWorker;

public sealed class PromptBuilder(string rubricTemplate, string rubricTailorTemplate)
{
    public const string KeywordsPlaceholder = "{{keywords}}";
    public const int MaxContextTokens = 16000;
    public const int CharsPerToken = 4;

    public const string RankingMemoryLabel = "## RANKING MEMORY";
    public const string ResumeMemoryLabel = "## RESUME MEMORY";
    public const string InventoryLabel = "## BLOCK INVENTORY";
    public const string SelectionLabel = "## CURRENT SELECTION";
    public const string ResumeLabel = "## CANDIDATE RESUME";
    public const string JobLabel = "## JOB POSTING";

    private static readonly JsonSerializerOptions KeywordJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public sealed record Prompt(string System, string User);

    public Prompt Compose(AiNextPayload next)
    {
        var rubric = rubricTemplate.Replace(KeywordsPlaceholder, KeywordsJson(next.Keywords), StringComparison.Ordinal);
        var system = string.Join("\n\n",
            rubric,
            $"{RankingMemoryLabel}\n(none confirmed yet)");
        if (!string.IsNullOrEmpty(next.Resume))
            system += $"\n\n{ResumeLabel}\n{next.Resume}";

        var budget = MaxContextTokens - Estimate(system);
        var content = TailTruncate(next.Content ?? string.Empty, budget);
        return new Prompt(system, $"{JobLabel}\n{content}");
    }

    public Prompt ComposeTailor(AiNextPayload next)
    {
        var rubric = rubricTailorTemplate.Replace(KeywordsPlaceholder, KeywordsJson(next.Keywords), StringComparison.Ordinal);
        var system = string.Join("\n\n",
            rubric,
            $"{ResumeMemoryLabel}\n(none confirmed yet)",
            $"{InventoryLabel}\n{InventoryJson(next.Inventory)}",
            $"{SelectionLabel}\n{(string.IsNullOrEmpty(next.Options) ? "{}" : next.Options)}");

        var budget = MaxContextTokens - Estimate(system);
        var content = TailTruncate(next.Content ?? string.Empty, budget);
        return new Prompt(system, $"{JobLabel}\n{content}");
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
