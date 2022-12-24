var builder = WebApplication.CreateBuilder(args);

// builder.Logging.ClearProviders();
// builder.Logging.AddConsole();

var app = builder.Build();

app.Logger.LogInformation("App started");
app.UseStaticFiles();
app.MapGet("/", () => "Hello World!");

app.Run();
