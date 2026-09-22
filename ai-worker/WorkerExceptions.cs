namespace AiWorker;

public sealed class CoreAbortException : Exception
{
    public CoreAbortException(string message) : base(message) { }
}

public sealed class LlmUnavailableException : Exception
{
    public LlmUnavailableException(string message) : base(message) { }
}

public sealed class LlmCallException : Exception
{
    public LlmCallException(string message) : base(message) { }
}

public sealed class ModelOutputException : Exception
{
    public string? Raw { get; }

    public ModelOutputException(string message, string? raw = null) : base(message)
    {
        Raw = raw;
    }
}
