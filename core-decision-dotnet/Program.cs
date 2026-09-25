using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Serilog;
using Serilog.Events;
using Photon.JobSeeker;

var builder = WebApplication.CreateBuilder(args);

var file_event_level = builder.Environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information;

var log_path = builder.Configuration["Logging:FilePath"] ?? "logs/E.log";
var log_directory = Path.GetDirectoryName(log_path);
if (!string.IsNullOrEmpty(log_directory)) Directory.CreateDirectory(log_directory);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(Serilog.Events.LogEventLevel.Debug)
    .WriteTo.File(log_path, file_event_level, rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

Log.Information("Starting up ...");

var database_factory = new DatabaseFactory(builder.Configuration);
Dictionaries.SetConfiguration(path: builder.Configuration["Database:Dictionaries"] ?? string.Empty);

var dashboard_key = builder.Configuration["Auth:ApiKeys:Dashboard"];
var search_key = builder.Configuration["Auth:ApiKeys:Search"];
var worker_key = builder.Configuration["Auth:ApiKeys:Worker"];
var assistant_key = builder.Configuration["Auth:ApiKeys:Assistant"];
var credential_key = builder.Configuration["Auth:CredentialKey"];
var api_clients = ApiAuthorization.FromConfig(dashboard_key, search_key, worker_key, assistant_key);

var auth_enabled = !string.IsNullOrEmpty(dashboard_key);

if (!auth_enabled)
{
    if (builder.Environment.IsProduction())
        throw new Exception("Auth:ApiKeys:Dashboard is required in production (set the Auth__ApiKeys__Dashboard environment variable).");

    Log.Warning("Auth:ApiKeys:Dashboard is not set - authentication is disabled (development only).");
}
else
{
    if (string.IsNullOrEmpty(search_key))
        Log.Warning("Auth:ApiKeys:Search is not set - the search extension cannot authenticate.");
    if (string.IsNullOrEmpty(worker_key))
        Log.Warning("Auth:ApiKeys:Worker is not set - ai-worker cannot authenticate.");
    if (string.IsNullOrEmpty(assistant_key))
        Log.Warning("Auth:ApiKeys:Assistant is not set - the assistant extension cannot authenticate.");
}

if (string.IsNullOrEmpty(credential_key))
{
    if (builder.Environment.IsProduction())
        throw new Exception("Auth:CredentialKey is required in production (set the Auth__CredentialKey environment variable).");

    Log.Warning("Auth:CredentialKey is not set - agency passwords remain plaintext (development only).");
}
else
{
    SecretProtector.SetKey(credential_key);

    using (var database = database_factory.Open())
    {
        AgencyBusiness.MigratePlaintextPasswords(database);
    }
}

using (var database = database_factory.Open())
{
    TrendBusiness.MigrateChallengeColumn(database);
}

builder.Services.AddRazorPages();
builder.Services.AddSingleton<IDatabaseFactory>(database_factory);
builder.Services.AddScoped(sp => sp.GetRequiredService<IDatabaseFactory>().Open());
builder.Services.AddScoped<TrendsCheckpoint>();
builder.Services.AddSingleton<Analyzer>();
builder.Services.AddSingleton<IViewRenderService, ViewRenderService>();
builder.Services.AddSingleton<MasterResumeCache>();
builder.Services.AddSingleton<ResumeInventoryCache>();
builder.Services.AddDataProtection();
builder.Services.AddHostedService<TrendsCleanupService>();

var app = builder.Build();

var protector = app.Services.GetRequiredService<IDataProtectionProvider>()
                        .CreateProtector(AuthOptions.ProtectorPurpose);

app.UseExceptionHandler(error_app => error_app.Run(async ctx =>
{
    var exception = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;

    if (exception is BadHttpRequestException bad_request)
    {
        ctx.Response.StatusCode = bad_request.StatusCode;
        return;
    }

    Log.Error(exception, "Unhandled exception on {0}", ctx.Request.Path);

    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;

    if (ctx.Request.Headers.Accept.ToString().Contains("application/json"))
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync("{\"error\":\"internal-server-error\"}");
    }
    else
    {
        ctx.Response.ContentType = "text/plain";
        await ctx.Response.WriteAsync("Internal server error");
    }
}));

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
});
app.UseRouting();

app.Use(async (ctx, next) =>
{
    if (Authorized(ctx)) await next();

    else if (ctx.Request.Headers.Accept.ToString().Contains("application/json"))
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync("{\"error\":\"unauthorized\"}");
    }
    else ctx.Response.Redirect("/auth/login");
});

app.MapRazorPages();
app.MapControllers();
app.Run();

bool Authorized(HttpContext ctx)
{
    if (!auth_enabled) return true;

    if (ctx.Request.Path.StartsWithSegments("/auth")) return true;

    var x_client = ctx.Request.Headers["X-Client"].ToString();
    if (x_client.Length > 0)
        Log.Debug("X-Client {Client} {Method} {Path}", x_client, ctx.Request.Method, ctx.Request.Path);

    var header_key = ctx.Request.Headers["X-API-Key"].ToString();

    if (header_key.Length > 0)
        return api_clients.TryAuthorize(header_key, ctx.Request.Path, ctx.Request.Method, out _);

    var cookie = ctx.Request.Cookies[AuthOptions.CookieName];

    if (!string.IsNullOrEmpty(cookie))
    {
        try
        {
            if (protector.Unprotect(cookie) == "ok") return true;
        }
        catch (CryptographicException) { }
    }

    return false;
}
