using Serilog;
using Photon.JobSeeker;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(builder.Configuration["Logging:FilePath"].ToString(), rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Information("Starting up ...");

Database.SetConfiguration(path: builder.Configuration["Database:Path"].ToString());

builder.Services.AddRazorPages();
builder.Services.AddSingleton<Trend>();
builder.Services.AddSingleton<Analyzer>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapRazorPages();
    endpoints.MapControllers();
});
app.Run();