using System.Text;
using Newtonsoft.Json;

namespace Photon.JobSeeker;

public static class SkillNormalizer
{
    public static readonly IReadOnlyDictionary<string, string> SeedAliases = new Dictionary<string, string>
    {
        ["js"] = "javascript",
        ["ts"] = "typescript",
        ["csharp"] = "c#",
        ["c sharp"] = "c#",
        ["c#.net"] = "c#",
        ["dotnet"] = ".net",
        [".net core"] = ".net",
        ["go"] = "golang",
        ["node"] = "node.js",
        ["nodejs"] = "node.js",
        ["reactjs"] = "react",
        ["react.js"] = "react",
        ["vuejs"] = "vue.js",
        ["ml"] = "machine learning",
        ["k8s"] = "kubernetes",
        ["kubernete"] = "kubernetes",
        ["jenkin"] = "jenkins",
        ["devop"] = "devops",
        ["postgre"] = "postgresql",
    };

    public static string SeedJson { get; } = JsonConvert.SerializeObject(SeedAliases, Formatting.Indented);

    public static string Normalize(string phrase, IReadOnlyDictionary<string, string> aliases)
    {
        var core = Core(phrase);
        if (core.Length == 0) return string.Empty;

        return aliases.TryGetValue(core, out var canonical) ? canonical.Trim().ToLowerInvariant() : core;
    }

    public static Dictionary<string, string> BuildAliasMap(IEnumerable<KeyValuePair<string, string>> raw)
    {
        var map = new Dictionary<string, string>();

        foreach (var (alias, canonical) in raw)
        {
            var key = Core(alias);
            var value = canonical?.Trim().ToLowerInvariant() ?? string.Empty;
            if (key.Length > 0 && value.Length > 0) map[key] = value;
        }

        return map;
    }

    public static HashSet<string> KnownSkills(ResumeContext resume, IReadOnlyDictionary<string, string> aliases)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, phrases) in resume.Keys)
        {
            Add(known, Normalize(key, aliases));

            foreach (var phrase in phrases ?? [])
                Add(known, Normalize(phrase, aliases));
        }

        return known;
    }

    private static void Add(HashSet<string> known, string skill)
    {
        if (skill.Length > 0) known.Add(skill);
    }

    private static string Core(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return string.Empty;

        var builder = new StringBuilder();

        foreach (var ch in phrase)
        {
            if (char.IsLetterOrDigit(ch) || ch == '#' || ch == '+' || ch == '.' || ch == '&')
                builder.Append(char.ToLowerInvariant(ch));
            else
                builder.Append(' ');
        }

        var collapsed = string.Join(' ',
            builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return collapsed.Length == 0 ? string.Empty : FoldPlural(collapsed);
    }

    private static string FoldPlural(string phrase)
    {
        var words = phrase.Split(' ');
        var last = words[^1];
        var dot = last.LastIndexOf('.');
        var stem = last[..(dot + 1)];
        var word = last[(dot + 1)..];

        if (word.Length >= 5 && word.EndsWith("ies")) word = word[..^3] + "y";
        else if (word.Length >= 5 && (word.EndsWith("xes") || word.EndsWith("zes"))) word = word[..^2];
        else if (word.Length >= 6 && (word.EndsWith("sses") || word.EndsWith("shes") || word.EndsWith("ches"))) word = word[..^2];
        else if (word.Length == 4 && word.EndsWith("is")) word = word[..^1];
        else if (word.Length >= 4 && word.EndsWith('s') && !word.EndsWith("ss") && !word.EndsWith("us") && !word.EndsWith("is")) word = word[..^1];

        words[^1] = stem + word;
        return string.Join(' ', words);
    }
}
