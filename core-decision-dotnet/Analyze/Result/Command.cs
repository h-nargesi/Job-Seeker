namespace Photon.JobSeeker;

public readonly struct Command
{
    public Command(PageAction action, string? @object, Dictionary<string, object>? @params)
    {
        page_action = action;
        Object = @object;
        Params = @params;
    }

    internal readonly PageAction page_action;

    public string Action => page_action.ToString();

    public string? Object { get; }

    public Dictionary<string, object>? Params { get; }

    public override string ToString()
    {
        var result = new List<string>(3)
        {
            $"action: {Action}"
        };
        if (Object != null) result.Add($"object: {Object}");
        if (Params != null) result.Add($"params: {Params.DictStringJoin()}");
        return $"{{{string.Join(", ", result)}}}";
    }

    public static Command Go(string url) => new(PageAction.go, null, new Dictionary<string, object>
    {
        [nameof(url)] = url,
    });

    public static Command Open(string url) => new(PageAction.open, null, new Dictionary<string, object>
    {
        [nameof(url)] = url,
    });

    public static Command Fill(string @object, object value) => new(PageAction.fill, @object, new Dictionary<string, object>
    {
        [nameof(value)] = value,
    });

    public static Command Click(string @object) => new(PageAction.click, @object, null);

    public static Command Recheck() => new(PageAction.recheck, null, null);

    public static Command Reload() => new(PageAction.reload, null, null);

    public static Command Close() => new(PageAction.close, null, null);

    public static Command Wait(int miliseconds) => new(PageAction.wait, null, new Dictionary<string, object>
    {
        [nameof(miliseconds)] = miliseconds,
    });
}