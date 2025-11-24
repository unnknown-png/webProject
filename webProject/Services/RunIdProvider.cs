namespace webProject.Services;

public interface IRunIdProvider
{
    string RunId { get; }
}

public class RunIdProvider : IRunIdProvider
{
    public RunIdProvider(string runId)
    {
        RunId = runId;
    }

    public string RunId { get; }
}

