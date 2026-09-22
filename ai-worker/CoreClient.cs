using System.Diagnostics;
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
        var watch = Stopwatch.StartNew();
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
            watch.Stop();
            WorkerLog.Debug("core GET /ai/next -> {Status} in {ElapsedMs} ms",
                (int)response.StatusCode, watch.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
                throw new CoreAbortException(
                    $"GET /ai/next returned {(int)response.StatusCode}: {await ErrorSnippetAsync(response, ct)}");

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

    public async Task<MemorySnapshot> FetchMemorySnapshotAsync(CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync("ai/memory", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CoreAbortException($"GET /ai/memory failed: {ex.Message}");
        }

        using (response)
        {
            watch.Stop();
            WorkerLog.Debug("core GET /ai/memory -> {Status} in {ElapsedMs} ms",
                (int)response.StatusCode, watch.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
                throw new CoreAbortException(
                    $"GET /ai/memory returned {(int)response.StatusCode}: {await ErrorSnippetAsync(response, ct)}");

            var body = await ReadAsync(response, ct);
            MemorySnapshot? snapshot;
            try
            {
                snapshot = JsonSerializer.Deserialize<MemorySnapshot>(body, Json);
            }
            catch (JsonException ex)
            {
                throw new CoreAbortException($"GET /ai/memory returned invalid JSON: {ex.Message}");
            }
            return snapshot ?? throw new CoreAbortException("GET /ai/memory returned null payload");
        }
    }

    public async Task<PostVerdictResult> PostVerdictAsync(long jobId, VerdictPayload verdict, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(verdict, Json);
        var watch = Stopwatch.StartNew();
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
            watch.Stop();
            WorkerLog.Debug("core POST /ai/verdict -> {Status} in {ElapsedMs} ms",
                (int)response.StatusCode, watch.ElapsedMilliseconds);

            var status = (int)response.StatusCode;
            if (status == 404) return PostVerdictResult.JobMissing;
            if (response.IsSuccessStatusCode) return PostVerdictResult.Accepted;

            var detail = await ErrorSnippetAsync(response, ct);
            throw new CoreAbortException(
                $"POST /ai/verdict returned {status} for job {jobId}: {detail}{(status == 400 ? " (worker bug)" : string.Empty)}");
        }
    }

    private static async Task<string> ErrorSnippetAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return WorkerLog.Snippet(await response.Content.ReadAsStringAsync(ct), 200);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"(body unreadable: {ex.Message})";
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
