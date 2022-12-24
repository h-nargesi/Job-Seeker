public struct Command
{
    public Command(Action action, string? @object, Dictionary<string, object>? @params)
    {
        Action = action;
        Object = @object;
        Params = @params;
    }

    public Action Action { get; }

    public string? Object { get; }

    public Dictionary<string, object>? Params { get; }

    public override string ToString()
    {
        return $"{{action: {Action}, object: {Object}, params: {Params}}}";
    }

    public static Command Go(string url) => new(Action.go, null, new Dictionary<string, object>
    {
        [nameof(url)] = url,
    });

    public static Command Open(string url) => new(Action.open, null, new Dictionary<string, object>
    {
        [nameof(url)] = url,
    });

    public static Command Fill(string @object, object value) => new(Action.fill, @object, new Dictionary<string, object>
    {
        [nameof(value)] = value,
    });

    public static Command Click(string @object) => new(Action.click, @object, null);

    public static Command Close() => new(Action.close, null, null);

    public static Command Wait(int miliseconds) => new(Action.wait, null, new Dictionary<string, object>
    {
        [nameof(miliseconds)] = miliseconds,
    });
}