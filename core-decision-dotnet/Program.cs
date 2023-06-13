using Serilog;
using Serilog.Events;
using Photon.JobSeeker;

var builder = WebApplication.CreateBuilder(args);

var file_event_level = builder.Environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(Serilog.Events.LogEventLevel.Debug)
    .WriteTo.File(builder.Configuration["Logging:FilePath"].ToString(), file_event_level, rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Information("Starting up ...");

Database.SetConfiguration(path: builder.Configuration["Database:Path"].ToString());
Dictionaries.SetConfiguration(path: builder.Configuration["Database:Dictionaries"].ToString());

builder.Services.AddRazorPages();
builder.Services.AddScoped<TrendsCheckpoint>();
builder.Services.AddSingleton<Analyzer>();
builder.Services.AddScoped<IViewRenderService, ViewRenderService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapRazorPages();
    endpoints.MapControllers();
});
app.Run();