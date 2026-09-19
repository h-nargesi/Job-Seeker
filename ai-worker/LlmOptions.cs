namespace AiWorker;

public sealed class LlmOptions
{
    public const double DefaultTemperature = 0.2;

    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public string Core { get; set; } = string.Empty;

    public string CoreApiKey { get; set; } = string.Empty;

    public string Rubric { get; set; } = string.Empty;

    public double Temperature { get; set; } = DefaultTemperature;

    public int Seed { get; set; }

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl)) return "Llm:BaseUrl is required";
        if (string.IsNullOrWhiteSpace(Model)) return "Llm:Model is required";
        if (string.IsNullOrWhiteSpace(Core)) return "Llm:Core is required";
        if (string.IsNullOrWhiteSpace(CoreApiKey)) return "Llm:CoreApiKey is required (Auth:ApiKeys:Worker)";
        if (string.IsNullOrWhiteSpace(Rubric)) return "Llm:Rubric is required";
        if (!Rubric.Contains(PromptBuilder.KeywordsPlaceholder, StringComparison.Ordinal))
            return $"Llm:Rubric must contain {PromptBuilder.KeywordsPlaceholder}";
        if (Temperature is < 0 or > 2) return "Llm:Temperature must be within 0-2";
        return null;
    }
}
