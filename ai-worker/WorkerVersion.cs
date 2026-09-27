using System.Reflection;

namespace AiWorker;

public static class WorkerVersion
{
    public static string Current => Get();

    private static string Get()
    {
        var assembly = typeof(WorkerVersion).Assembly;
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        return attribute?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
