using AiWorker;
using Microsoft.Extensions.Configuration;

#if DEBUG
const string default_environment = "Development";
#else
const string default_environment = "Production";
#endif
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? default_environment;

Console.WriteLine($"[worker] environment: {environment}");

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var options = config.GetSection("Llm").Get<LlmOptions>() ?? new LlmOptions();
var config_error = options.Validate();
if (config_error is not null)
{
    Console.Error.WriteLine($"[worker] configuration error: {config_error}");
    return 4;
}

using var core_http = new HttpClient { BaseAddress = new Uri(Base(options.Core)) };
using var llm_http = new HttpClient { BaseAddress = new Uri(Base(options.BaseUrl)) };

var worker = new WorkerLoop(
    new CoreClient(core_http, options.CoreApiKey),
    new LlmClient(llm_http, options),
    new PromptBuilder(options.Rubric, options.RubricTailor));

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cancellation.Cancel();
};

try
{
    return await worker.RunAsync(cancellation.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("[worker] interrupted - stopping");
    return WorkerLoop.ExitInterrupted;
}

static string Base(string url)
{
    return url.TrimEnd('/') + "/";
}
