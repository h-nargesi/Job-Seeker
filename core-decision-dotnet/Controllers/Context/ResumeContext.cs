namespace Photon.JobSeeker;

[Serializable]
public class ResumeContext
{
    public int Length { get; set; } = 1;
    public KeysContext Keys { get; } = new();
    public NotIncludedContext NotIncluded { get; } = new();
    public ElementsContext Elements { get; } = new();

    [Serializable]
    public class KeysContext
    {
        public bool DOTNET { get; set; } = true;
        public bool JAVA { get; set; } = true;
        public bool PYTHON { get; set; }
        public bool GOLANG { get; set; }
        public bool SQL { get; set; } = true;
        public bool FRONT_END { get; set; } = true;
        public bool WEB { get; set; } = true;
        public bool MACHINE_LEARNING { get; set; }
        public bool NETWORK { get; set; }

        public Dictionary<string, string[]>? More { get; set; }
    }

    [Serializable]
    public class NotIncludedContext : HashSet<string>
    {
        public const string ClearanceMonitoring = "clearance-monitoring";
    }

    [Serializable]
    public class ElementsContext
    {
        public bool LOCATION { get; set; }
        public bool LINKEDIN { get; set; } = true;
        public bool FOOTER { get; set; } = true;
    }

    public string FileName()
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

        var temp = string.Join("", keys);
        keys.Clear();
        keys.Add(temp);
        keys.AddRange(Keys?.More?.Keys.Select(x => x.ToLower()) ?? new string[0]);

        return "hamed-nargesi-resume-" + string.Join("-", keys) + ".pdf";
    }
}

public static class ResumeContextExtentions
{
    public static string JS(this bool value)
    {
        return value.ToString().ToLower();
    }
}
