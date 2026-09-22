using AiWorker;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

#if DEBUG
const string default_environment = "Development";
#else
const string default_environment = "Production";
#endif
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? default_environment;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var file_level = FileLevel(config["Logging:MinimumLevel"], environment);
var log_path = config["Logging:FilePath"] ?? "logs/worker.log";
var log_directory = Path.GetDirectoryName(log_path);
if (!string.IsNullOrEmpty(log_directory)) Directory.CreateDirectory(log_directory);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(LogEventLevel.Debug)
    .WriteTo.File(log_path, file_level, rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    WorkerLog.Info("worker starting - environment: {Environment}", environment);

    var options = config.GetSection("Llm").Get<LlmOptions>() ?? new LlmOptions();
    var config_error = options.Validate();
    if (config_error is not null)
    {
        WorkerLog.Error("configuration error: {Error}", config_error);
        return 4;
    }

    using var core_http = new HttpClient { BaseAddress = new Uri(Base(options.Core)) };
    using var llm_http = new HttpClient { BaseAddress = new Uri(Base(options.BaseUrl)) };

    var worker = new WorkerLoop(
        new CoreClient(core_http, options.CoreApiKey),
        new LlmClient(llm_http, options),
        new PromptBuilder(options.Rubric, options.RubricTailor, options.MaxCompletionTokens),
        options);

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
        WorkerLog.Info("interrupted - stopping");
        return WorkerLoop.ExitInterrupted;
    }
    catch (Exception ex)
    {
        WorkerLog.Error(ex, "unexpected error - aborting run");
        return WorkerLoop.ExitUnexpected;
    }
}
finally
{
    Log.CloseAndFlush();
}

static LogEventLevel FileLevel(string? configured, string environment)
{
    if (configured is not null && Enum.TryParse(configured, true, out LogEventLevel parsed))
        return parsed;
    return environment == "Development" ? LogEventLevel.Debug : LogEventLevel.Information;
}

static string Base(string url)
{
    return url.TrimEnd('/') + "/";
}
