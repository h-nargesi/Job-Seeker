using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Photon.JobSeeker;

[Serializable]
public partial class ResumeContext
{
    public int Version => 42;
    public int Length { get; set; } = 1;
    public InputDataContext InputData { get; } = new()
    {
        { nameof(InputDataContext.BACK_END_EXP), InputDataContext.BACK_END_EXP_DEFAULT },
        { nameof(InputDataContext.FRONT_END_EXP), InputDataContext.FRONT_END_EXP_DEFAULT }
    };
    public KeysContext Keys { get; } = new()
    {
        { nameof(KeysContext.DOTNET), null },
        { nameof(KeysContext.JAVA), null },
        { nameof(KeysContext.PYTHON), null },
        { nameof(KeysContext.GOLANG), null },
        { nameof(KeysContext.SQL), null },
        { nameof(KeysContext.FRONT_END), null },
        { nameof(KeysContext.WEB), null },
        { nameof(KeysContext.MACHINE_LEARNING), null },
        { nameof(KeysContext.NETWORK), null },
    };
    public NotIncludedContext PageBreak { get; } = [];
    public NotIncludedContext NotIncluded { get; } = [];
    public NotIncludedContext Included { get; } = [];
    public ElementsContext Elements { get; } = new();

    [Serializable]
    public class InputDataContext : Dictionary<string, object?>
    {
        public const int BACK_END_EXP_DEFAULT = 14;
        public const int FRONT_END_EXP_DEFAULT = 3;

        public object BACK_END_EXP => TryGetValue(nameof(BACK_END_EXP), BACK_END_EXP_DEFAULT);
        public object FRONT_END_EXP => TryGetValue(nameof(FRONT_END_EXP), FRONT_END_EXP_DEFAULT);

        public object TryGetValue(string key, object default_value)
        {
            TryGetValue(key, out var value);
            return value ?? default_value;
        }
    }

    [Serializable]
    public class KeysContext : Dictionary<string, HashSet<string>?>
    {
        public bool DOTNET => ContainsKey(nameof(DOTNET)) && this[nameof(DOTNET)] != null;
        public bool JAVA => ContainsKey(nameof(JAVA)) && this[nameof(JAVA)] != null;
        public bool PYTHON => ContainsKey(nameof(PYTHON)) && this[nameof(PYTHON)] != null;
        public bool GOLANG => ContainsKey(nameof(GOLANG)) && this[nameof(GOLANG)] != null;
        public bool SQL => ContainsKey(nameof(SQL)) && this[nameof(SQL)] != null;
        public bool FRONT_END => ContainsKey(nameof(FRONT_END)) && this[nameof(FRONT_END)] != null;
        public bool WEB => ContainsKey(nameof(WEB)) && this[nameof(WEB)] != null;
        public bool MACHINE_LEARNING => ContainsKey(nameof(MACHINE_LEARNING)) && this[nameof(MACHINE_LEARNING)] != null;
        public bool NETWORK => ContainsKey(nameof(NETWORK)) && this[nameof(NETWORK)] != null;

        public int SubLength()
        {
            var count = 0;
            foreach(var l in this) count += l.Value?.Count ?? 0;
            return count;
        }

        public static readonly IReadOnlySet<string> MainKeys = new HashSet<string>()
        {
            nameof(DOTNET),
            nameof(JAVA),
            nameof(PYTHON),
            nameof(GOLANG),
            nameof(SQL),
            nameof(FRONT_END),
            nameof(WEB),
            nameof(MACHINE_LEARNING),
            nameof(NETWORK),
        };

        public static readonly IReadOnlySet<string> NotInclude = new HashSet<string>()
        {
            "angular", "database"
        };

        public string ToSimpleJson()
        {
            var result = this.Where(x => x.Value != null)
                             .Select(x => $"{x.Key}: [{string.Join(", ", x.Value!.Select(v => "'" + v + "'"))}]");
            return string.Join(",\n\t\t", result);
        }
    }

    [Serializable]
    public class NotIncludedContext : HashSet<string>
    {
        public const string ClearanceMonitoring = "#clearance-monitoring";

        public string ToSimpleJson()
        {
            return string.Join(",\n\t\t", this.Select(x => "'" + x + "'"));
        }
    }

    [Serializable]
    public class ElementsContext
    {
        public bool IMAGE { get; set; } = true;
        public bool LOCATION { get; set; } = true;
        public bool PHONE { get; set; } = true;
        public bool SKYPE { get; set; } = false;
        public bool LINKEDIN { get; set; } = true;
        public bool FOOTER { get; set; } = true;
        public string IMAGE_URL { get; set; } = "/images/portrate.png";
        public string GetImageURL() => !string.IsNullOrWhiteSpace(IMAGE_URL) ? IMAGE_URL : "/images/portrate.png";
    }

    public string FileName(string extnesion)
    {
        return $"hamed-nargesi-resume-{Version}-{GetKeywords()}.{extnesion}";
    }

    public void CheckSize()
    {
        var keywords = GetKeywords();

        if (keywords == "js" && Keys.SubLength() <= 2)
        {
            Length = 2;
            Elements.FOOTER = false;
        }

        if (keywords.Contains('d') && keywords.Contains('j'))
        {
            NotIncluded.Add(NotIncludedContext.ClearanceMonitoring);
        }
    }

    private string GetKeywords()
    {
        var keys = new List<string>();

        if (Keys.DOTNET) keys.Add("d");
        if (Keys.JAVA) keys.Add("j");
        if (Keys.PYTHON) keys.Add("p");
        if (Keys.GOLANG) keys.Add("g");
        if (Keys.SQL) keys.Add("s");
        if (Keys.FRONT_END) keys.Add("f");
        if (Keys.WEB) keys.Add("w");
        if (Keys.MACHINE_LEARNING) keys.Add("m");
        if (Keys.NETWORK) keys.Add("n");

        return string.Join("", keys);
    }

    public string SimlpeSerialize()
    {
        var json =  JsonConvert.SerializeObject(this, Formatting.Indented);
        return SerializeChecktSyntaxt(json);
    }

    public static ResumeContext? SimlpeDeserialize(string text)
    {
        if (text == null) return null;

        text = DeserializeChecktSyntaxt(text);

        return JsonConvert.DeserializeObject<ResumeContext>(text);
    }

    private static string DeserializeChecktSyntaxt(string text)
    {
        var is_qouted = false;
        var parts = text.Split("\"")
            .Select(part =>
            {
                if (!is_qouted)
                    part = QouteDeserializer().Replace(part, "\"$0\"");

                is_qouted = !is_qouted;
                return part;
            });

        return string.Join("\"", parts);
    }

    private static string SerializeChecktSyntaxt(string json)
    {
        return QouteSerializer().Replace(json, "$1");
    }

    [GeneratedRegex(@"\w+(?=\s*:)")]
    private static partial Regex QouteDeserializer();

    [GeneratedRegex(@"""(\w+)""(?=\s*:)")]
    private static partial Regex QouteSerializer();
}

public static class ResumeContextExtentions
{
    public static string JS(this bool value)
    {
        return value.ToString().ToLower();
    }

    public static string JS(this string value)
    {
        return $"'{value}'";
    }
}
