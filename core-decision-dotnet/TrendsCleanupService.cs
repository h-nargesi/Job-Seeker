using Serilog;

namespace Photon.JobSeeker;

class TrendsCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Cleanup();

        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }

            Cleanup();
        }
    }

    private static void Cleanup()
    {
        try
        {
            using var database = Database.Open();
            database.Trend.DeleteExpired();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Trends cleanup failed");
        }
    }
}
