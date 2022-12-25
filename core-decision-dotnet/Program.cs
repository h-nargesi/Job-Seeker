var builder = WebApplication.CreateBuilder(args);

// builder.Logging.ClearProviders();
// builder.Logging.AddConsole();
builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<Agency>();
builder.Services.AddSingleton<Trend>();
builder.Services.AddSingleton<Analyzer>();

var app = builder.Build();

app.Logger.LogInformation("App started");
app.UseStaticFiles();
app.MapGet("/", () => "Hello World!");

app.Run();
