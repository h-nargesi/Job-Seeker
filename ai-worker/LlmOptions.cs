namespace AiWorker;

public sealed class LlmOptions
{
    public const double DefaultTemperature = 0.2;
    public const int DefaultTimeoutSeconds = 120;
    public const int DefaultMaxCompletionTokens = 2048;
    public const int DefaultSlot = 0;

    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public string Core { get; set; } = string.Empty;

    public string CoreApiKey { get; set; } = string.Empty;

    public string Fixed { get; set; } = string.Empty;

    public string Rubric { get; set; } = string.Empty;

    public string RubricTailor { get; set; } = string.Empty;

    public double Temperature { get; set; } = DefaultTemperature;

    public int Seed { get; set; }

    public int Slot { get; set; } = DefaultSlot;

    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    public int MaxCompletionTokens { get; set; } = DefaultMaxCompletionTokens;

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl)) return "Llm:BaseUrl is required";
        if (string.IsNullOrWhiteSpace(Model)) return "Llm:Model is required";
        if (string.IsNullOrWhiteSpace(Core)) return "Llm:Core is required";
        if (string.IsNullOrWhiteSpace(CoreApiKey)) return "Llm:CoreApiKey is required (Auth:ApiKeys:Worker)";
        if (string.IsNullOrWhiteSpace(Fixed)) return "Llm:Fixed is required";
        if (string.IsNullOrWhiteSpace(Rubric)) return "Llm:Rubric is required";
        if (string.IsNullOrWhiteSpace(RubricTailor)) return "Llm:RubricTailor is required";
        if (Temperature is < 0 or > 2) return "Llm:Temperature must be within 0-2";
        if (Slot is < 0 or > 999) return "Llm:Slot must be 0-999";
        if (TimeoutSeconds is < 5 or > 3600) return "Llm:TimeoutSeconds must be 5-3600";
        if (MaxCompletionTokens is < 128 or > 8192) return "Llm:MaxCompletionTokens must be 128-8192";
        return null;
    }
}
