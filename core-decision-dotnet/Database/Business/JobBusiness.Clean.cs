using Serilog;

namespace Photon.JobSeeker
{
    partial class JobBusiness
    {
        public static CleanProcess? CurrentCleanProcess { get; private set; }

        private static readonly object clean_lock = new();

        public void Clean(int months, Action<int, string>? progress = null)
        {
            progress?.Invoke(0, "deleting old jobs");
            database.Execute(Q_CLEAN, new { date = DateTime.Now.AddMonths(-months) });

            progress?.Invoke(17, "trimming attention html");
            database.Execute(Q_CLEAN_ATTENTION, RankingParameters());

            progress?.Invoke(33, "trimming rejected html");
            database.Execute(Q_CLEAN_NOT_APPROVED);

            progress?.Invoke(50, "purging below-floor ai content");
            database.Execute(Q_CLEAN_AI_BELOW_PURGE_FLOOR,
                new { aiPurgeFloor = database.AppSetting.AiPurgeFloor() });

            progress?.Invoke(67, "purging below-floor regex content");
            database.Execute(Q_CLEAN_REGEX_BELOW_FLOOR,
                new { floor = database.AppSetting.Floor() });

            progress?.Invoke(83, "vacuuming");
            database.Execute(Q_VACUUM);

            progress?.Invoke(100, "done");
        }

        internal static bool TryBeginClean(out CleanProcess process)
        {
            lock (clean_lock)
            {
                if (CurrentCleanProcess is { Running: true })
                {
                    process = CurrentCleanProcess;
                    return false;
                }

                process = new CleanProcess { Running = true, Percent = 0, Stage = "starting" };
                CurrentCleanProcess = process;
                return true;
            }
        }

        public static CleanProcess StartClean(IDatabaseFactory factory)
        {
            if (!TryBeginClean(out var process)) return process;

            _ = Task.Run(() =>
            {
                try
                {
                    using var database = factory.Open();
                    database.Job.Clean(1, (percent, stage) =>
                    {
                        lock (clean_lock)
                        {
                            process.Percent = percent;
                            process.Stage = stage;
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(string.Join("\r\n", ex.Message, ex.StackTrace));
                    lock (clean_lock) process.Error = ex.Message;
                }
                finally
                {
                    lock (clean_lock) process.Running = false;
                }
            });

            return process;
        }

        public sealed class CleanProcess
        {
            public bool Running { get; internal set; }

            public int Percent { get; internal set; }

            public string Stage { get; internal set; } = "starting";

            public string? Error { get; internal set; }

            internal object ToReport()
            {
                lock (clean_lock)
                {
                    return new
                    {
                        running = Running,
                        percent = Percent,
                        stage = Stage,
                        error = Error,
                    };
                }
            }
        }
    }
}
