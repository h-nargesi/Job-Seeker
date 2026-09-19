using System.Text;
using System.Text.Json;

namespace AiWorker;

public sealed class LlmClient
{
    public const int TimeoutSeconds = 120;

    private readonly HttpClient http;
    private readonly LlmOptions options;

    public LlmClient(HttpClient http, LlmOptions options)
    {
        this.http = http;
        this.options = options;
        http.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        if (!string.IsNullOrEmpty(options.ApiKey))
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
    }

    public Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        return CompleteAsync(system, user, VerdictSchema.ResponseFormat, ct);
    }

    public async Task<string> CompleteAsync(string system, string user, string responseFormat, CancellationToken ct)
    {
        var request = new Dictionary<string, object?>
        {
            ["model"] = options.Model,
            ["messages"] = new object[]
            {
                new Dictionary<string, object?> { ["role"] = "system", ["content"] = system },
                new Dictionary<string, object?> { ["role"] = "user", ["content"] = user },
            },
            ["temperature"] = options.Temperature,
            ["seed"] = options.Seed,
            ["stream"] = false,
            ["response_format"] = JsonDocument.Parse(responseFormat).RootElement.Clone(),
        };
        var body = JsonSerializer.Serialize(request);

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync("chat/completions",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LlmUnavailableException($"model endpoint unreachable: {ex.Message}");
        }

        using (response)
        {
            string raw;
            try
            {
                raw = await response.Content.ReadAsStringAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new LlmUnavailableException($"model response read failed: {ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
                throw new LlmUnavailableException(
                    $"model endpoint returned {(int)response.StatusCode}: {Snippet(raw)}");

            return ExtractContent(raw);
        }
    }

    internal static string ExtractContent(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var message = choices[0];
                if (message.ValueKind == JsonValueKind.Object &&
                    message.TryGetProperty("message", out var header) &&
                    header.ValueKind == JsonValueKind.Object &&
                    header.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String)
                    return content.GetString()!;
            }
        }
        catch (JsonException)
        {
        }
        throw new ModelOutputException("chat response missing choices[0].message.content");
    }

    private static string Snippet(string text)
    {
        return text.Length <= 200 ? text : $"{text[..200]}...";
    }
}
