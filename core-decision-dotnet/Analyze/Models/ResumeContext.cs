using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Photon.JobSeeker;

[Serializable]
public class ResumeContext
{
    public const int Version = 39;
    public int Length { get; set; } = 1;
    public KeysContext Keys { get; } = new()
    {
        { nameof(ResumeContext.KeysContext.DOTNET), null },
        { nameof(ResumeContext.KeysContext.JAVA), null },
        { nameof(ResumeContext.KeysContext.PYTHON), null },
        { nameof(ResumeContext.KeysContext.GOLANG), null },
        { nameof(ResumeContext.KeysContext.SQL), null },
        { nameof(ResumeContext.KeysContext.FRONT_END), null },
        { nameof(ResumeContext.KeysContext.WEB), null },
        { nameof(ResumeContext.KeysContext.MACHINE_LEARNING), null },
        { nameof(ResumeContext.KeysContext.NETWORK), null },
    };
    public NotIncludedContext NotIncluded { get; } = new();
    public ElementsContext Elements { get; } = new();

    [Serializable]
    public class KeysContext : Dictionary<string, HashSet<string>?>
    {
        public bool DOTNET => this.ContainsKey(nameof(DOTNET)) && this[nameof(DOTNET)] != null;
        public bool JAVA => this.ContainsKey(nameof(JAVA)) && this[nameof(JAVA)] != null;
        public bool PYTHON => this.ContainsKey(nameof(PYTHON)) && this[nameof(PYTHON)] != null;
        public bool GOLANG => this.ContainsKey(nameof(GOLANG)) && this[nameof(GOLANG)] != null;
        public bool SQL => this.ContainsKey(nameof(SQL)) && this[nameof(SQL)] != null;
        public bool FRONT_END => this.ContainsKey(nameof(FRONT_END)) && this[nameof(FRONT_END)] != null;
        public bool WEB => this.ContainsKey(nameof(WEB)) && this[nameof(WEB)] != null;
        public bool MACHINE_LEARNING => this.ContainsKey(nameof(MACHINE_LEARNING)) && this[nameof(MACHINE_LEARNING)] != null;
        public bool NETWORK => this.ContainsKey(nameof(NETWORK)) && this[nameof(NETWORK)] != null;

        public int SubLength()
        {
            var count = 0;
            foreach(var l in this) count += l.Value?.Count ?? 0;
            return count;
        }

        public static readonly IReadOnlySet<string> MainKeys = new HashSet<string>()
        {
            nameof(ResumeContext.KeysContext.DOTNET),
            nameof(ResumeContext.KeysContext.JAVA),
            nameof(ResumeContext.KeysContext.PYTHON),
            nameof(ResumeContext.KeysContext.GOLANG),
            nameof(ResumeContext.KeysContext.SQL),
            nameof(ResumeContext.KeysContext.FRONT_END),
            nameof(ResumeContext.KeysContext.WEB),
            nameof(ResumeContext.KeysContext.MACHINE_LEARNING),
            nameof(ResumeContext.KeysContext.NETWORK),
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
        public const string ClearanceMonitoring = "clearance-monitoring";

        public string ToSimpleJson()
        {
            return string.Join(",\n\t\t", this.Select(x => "'" + x + "'"));
        }
    }

    [Serializable]
    public class ElementsContext
    {
        public bool LOCATION { get; set; }
        public bool PHONE { get; set; } = true;
        public bool LINKEDIN { get; set; } = true;
        public bool FOOTER { get; set; } = true;
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
        return JsonConvert.SerializeObject(this, Formatting.Indented).Replace("\"", "");
    }

    public static ResumeContext? SimlpeDeserialize(string text)
    {
        if (text == null) return null;

        text = new Regex(@"\b(?!true|false|null)[\w \-\/\.]+").Replace(text, "\"$0\"");

        return JsonConvert.DeserializeObject<ResumeContext>(text);
    }
}

public static class ResumeContextExtentions
{
    public static string JS(this bool value)
    {
        return value.ToString().ToLower();
    }
}
