namespace AiWorker;

public sealed class CoreAbortException : Exception
{
    public CoreAbortException(string message) : base(message) { }
}

public sealed class LlmUnavailableException : Exception
{
    public LlmUnavailableException(string message) : base(message) { }
}

public sealed class ModelOutputException : Exception
{
    public ModelOutputException(string message) : base(message) { }
}
