using Serilog;
using Photon.JobSeeker;

var builder = WebApplication.CreateBuilder(args);

var log_path = System.IO.Path.Combine(
    builder.Environment.ContentRootPath, 
    builder.Configuration["Logging:LogFile:Path"].ToString());

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(log_path, rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Information("Starting up");

builder.Services.AddRazorPages();
builder.Services.AddSingleton<Database>();
// builder.Services.AddSingleton<Agency>();
builder.Services.AddSingleton<Trend>();
builder.Services.AddSingleton<Analyzer>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.Run();
