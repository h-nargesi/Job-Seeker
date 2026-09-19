using System.Text;
using System.Text.Json;

namespace AiWorker;

public enum PostVerdictResult
{
    Accepted,
    JobMissing,
}

public sealed class CoreClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient http;

    public CoreClient(HttpClient http, string apiKey)
    {
        this.http = http;
        this.http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        this.http.DefaultRequestHeaders.Add("X-Client", "worker");
    }

    public async Task<AiNextPayload> FetchNextAsync(CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync("ai/next", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CoreAbortException($"GET /ai/next failed: {ex.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new CoreAbortException($"GET /ai/next returned {(int)response.StatusCode}");

            var body = await ReadAsync(response, ct);
            AiNextPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<AiNextPayload>(body, Json);
            }
            catch (JsonException ex)
            {
                throw new CoreAbortException($"GET /ai/next returned invalid JSON: {ex.Message}");
            }
            return payload ?? throw new CoreAbortException("GET /ai/next returned null payload");
        }
    }

    public async Task<PostVerdictResult> PostVerdictAsync(long jobId, VerdictPayload verdict, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(verdict, Json);
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync($"ai/verdict?jobid={jobId}",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CoreAbortException($"POST /ai/verdict failed for job {jobId}: {ex.Message}");
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (status == 404) return PostVerdictResult.JobMissing;
            if (response.IsSuccessStatusCode) return PostVerdictResult.Accepted;

            var detail = await ReadAsync(response, ct);
            throw new CoreAbortException(
                $"POST /ai/verdict returned {status} for job {jobId}{(status == 400 ? $" (worker bug): {detail}" : string.Empty)}");
        }
    }

    private static async Task<string> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CoreAbortException($"core response read failed: {ex.Message}");
        }
    }
}
