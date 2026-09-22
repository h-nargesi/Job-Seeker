using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AiWorker;

public sealed record LlmResult(
    string Content,
    int? PromptTokens,
    int? CompletionTokens,
    string? FinishReason,
    long ElapsedMs);

public sealed class LlmClient
{
    private readonly HttpClient http;
    private readonly LlmOptions options;

    public LlmClient(HttpClient http, LlmOptions options)
    {
        this.http = http;
        this.options = options;
        http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        if (!string.IsNullOrEmpty(options.ApiKey))
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
    }

    public Task<LlmResult> CompleteAsync(string system, string user, CancellationToken ct)
    {
        return CompleteAsync(system, user, VerdictSchema.ResponseFormat, 0, ct);
    }

    public async Task<LlmResult> CompleteAsync(string system, string user, string responseFormat, int seedOffset, CancellationToken ct)
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
            ["seed"] = options.Seed + seedOffset,
            ["stream"] = false,
            ["max_tokens"] = options.MaxCompletionTokens,
            ["response_format"] = JsonDocument.Parse(responseFormat).RootElement.Clone(),
        };
        var body = JsonSerializer.Serialize(request);
        var watch = Stopwatch.StartNew();

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync("chat/completions",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LlmCallException($"model generation timed out after {options.TimeoutSeconds}s");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new LlmUnavailableException($"model endpoint unreachable: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new LlmCallException($"model call failed before a response: {ex.Message}");
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
                throw new LlmCallException($"model response read failed: {ex.Message}");
            }

            watch.Stop();

            if (!response.IsSuccessStatusCode)
                throw new LlmCallException(
                    $"model endpoint returned {(int)response.StatusCode}: {Snippet(raw)}");

            return Extract(raw) with { ElapsedMs = watch.ElapsedMilliseconds };
        }
    }

    internal static LlmResult Extract(string raw)
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
                var choice = choices[0];
                if (choice.ValueKind == JsonValueKind.Object &&
                    choice.TryGetProperty("message", out var header) &&
                    header.ValueKind == JsonValueKind.Object &&
                    header.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String)
                {
                    return new LlmResult(
                        content.GetString()!,
                        TokenCount(root, "prompt_tokens"),
                        TokenCount(root, "completion_tokens"),
                        choice.TryGetProperty("finish_reason", out var finish) && finish.ValueKind == JsonValueKind.String
                            ? finish.GetString()
                            : null,
                        0);
                }
            }
        }
        catch (JsonException)
        {
        }
        throw new ModelOutputException("chat response missing choices[0].message.content", raw);
    }

    private static int? TokenCount(JsonElement root, string property)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return null;
        if (!usage.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number)
            return null;
        return value.TryGetInt32(out var tokens) ? tokens : null;
    }

    private static string Snippet(string text)
    {
        return text.Length <= 200 ? text : $"{text[..200]}...";
    }
}
