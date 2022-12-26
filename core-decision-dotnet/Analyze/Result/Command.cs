namespace Photon.JobSeeker
{
    public struct Command
    {
        public Command(PageAction action, string? @object, Dictionary<string, object>? @params)
        {
            Action = action.ToString();
            Object = @object;
            Params = @params;
        }

        public string Action { get; }

        public string? Object { get; }

        public Dictionary<string, object>? Params { get; }

        public override string ToString()
        {
            return $"{{action: {Action}, object: {Object}, params: {Params}}}";
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

        public static Command Close() => new(PageAction.close, null, null);

        public static Command Wait(int miliseconds) => new(PageAction.wait, null, new Dictionary<string, object>
        {
            [nameof(miliseconds)] = miliseconds,
        });


        public static Command[] JustClose() => new Command[] { Close() };
    }
}