namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public enum G0StepStatus
{
    Passed,
    Failed,
    BlockedDependency,
    SkippedDependency,
    NotRun
}

public sealed record G0StepObservation(
    string Expected,
    string Actual,
    G0StepStatus Status = G0StepStatus.Passed,
    IReadOnlyList<string>? Evidence = null);

public sealed record G0StepResult(
    string Id,
    string Name,
    G0StepStatus Status,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    string Expected,
    string Actual,
    string? Error,
    IReadOnlyList<string> Evidence)
{
    public double DurationMilliseconds => (CompletedUtc - StartedUtc).TotalMilliseconds;
}

public sealed class G0RunResult
{
    public string Gate { get; init; } = "G0";
    public int ExpectedStepCount { get; init; } = 25;
    public required string RunId { get; init; }
    public required string Environment { get; init; }
    public required DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset CompletedUtc { get; set; }
    public string? ApiProcessId { get; set; }
    public string? DesktopProcessId { get; set; }
    public string ApiExecutable { get; init; } = string.Empty;
    public string DesktopExecutable { get; init; } = string.Empty;
    public Dictionary<string, string> Endpoints { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public bool CleanupSucceeded { get; set; }
    public List<G0StepResult> Steps { get; } = [];

    public bool Passed => CleanupSucceeded
        && Steps.Count == ExpectedStepCount
        && Steps.All(step => step.Status == G0StepStatus.Passed);
}

public sealed class G0DependencyException(
    string message,
    G0StepStatus status = G0StepStatus.BlockedDependency) : Exception(message)
{
    public G0StepStatus Status { get; } = status;
}

public sealed class G0AuditRecorder(G0RunResult result)
{
    public G0RunResult Result { get; } = result;

    public async Task<G0StepResult> RunAsync(
        string id,
        string name,
        string expected,
        Func<CancellationToken, Task<G0StepObservation>> action,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        G0StepResult step;
        try
        {
            var observation = await action(cancellationToken).ConfigureAwait(false);
            step = new G0StepResult(
                id,
                name,
                observation.Status,
                started,
                DateTimeOffset.UtcNow,
                string.IsNullOrWhiteSpace(observation.Expected) ? expected : observation.Expected,
                observation.Actual,
                null,
                observation.Evidence ?? []);
        }
        catch (G0DependencyException exception)
        {
            step = new G0StepResult(
                id,
                name,
                exception.Status,
                started,
                DateTimeOffset.UtcNow,
                expected,
                exception.Message,
                null,
                []);
        }
        catch (Exception exception)
        {
            step = new G0StepResult(
                id,
                name,
                G0StepStatus.Failed,
                started,
                DateTimeOffset.UtcNow,
                expected,
                exception.Message,
                exception.ToString(),
                []);
        }

        Result.Steps.Add(step);
        return step;
    }
}
