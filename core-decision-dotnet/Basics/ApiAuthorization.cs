using Microsoft.AspNetCore.Http;

namespace Photon.JobSeeker;

public sealed class ApiPathRule
{
    public required string Prefix { get; init; }

    public string? Method { get; init; }
}

public sealed class ApiClientRole
{
    public required string Role { get; init; }

    public string? Secret { get; init; }

    public IReadOnlyList<ApiPathRule> Rules { get; init; } = [];
}

public sealed class ApiAuthorization(IReadOnlyList<ApiClientRole> clients)
{
    public const string DashboardRole = "dashboard";
    public const string SearchRole = "search";
    public const string WorkerRole = "worker";

    public IReadOnlyList<ApiClientRole> Clients { get; } = clients;

    public static ApiAuthorization FromConfig(string? dashboard, string? search, string? worker)
    {
        return new ApiAuthorization(
        [
            new ApiClientRole { Role = DashboardRole, Secret = dashboard, Rules = [] },
            new ApiClientRole { Role = SearchRole, Secret = search, Rules = [new ApiPathRule { Prefix = "/decision" }] },
            new ApiClientRole { Role = WorkerRole, Secret = worker, Rules = [new ApiPathRule { Prefix = "/ai" }] },
        ]);
    }

    public bool TryAuthorize(string apiKey, PathString path, string method, out string role)
    {
        role = string.Empty;
        if (string.IsNullOrEmpty(apiKey)) return false;

        foreach (var client in Clients)
        {
            if (!KeyEquals(apiKey, client.Secret)) continue;
            role = client.Role;
            return PathAllowed(path, method, client.Rules);
        }

        return false;
    }

    public static bool PathAllowed(PathString path, string method, IReadOnlyList<ApiPathRule> rules)
    {
        if (rules.Count == 0) return true;

        foreach (var rule in rules)
        {
            if (!path.StartsWithSegments(rule.Prefix)) continue;
            if (rule.Method != null &&
                !string.Equals(rule.Method, method, StringComparison.OrdinalIgnoreCase))
                continue;
            return true;
        }

        return false;
    }

    public static bool KeyEquals(string provided, string? configured)
    {
        if (string.IsNullOrEmpty(configured)) return false;
        if (provided.Length != configured.Length) return false;
        return AuthController.FixedTimeEquals(provided, configured);
    }
}
