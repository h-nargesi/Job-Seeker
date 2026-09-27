using System.Reflection;

namespace Photon.JobSeeker;

public static class AppVersion
{
    public static string Current => Get();

    private static string Get()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        return attribute?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
