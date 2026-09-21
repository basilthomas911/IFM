namespace TomasAI.IFM.Application.Api.Server;

/// <summary>One-shot process-local handoff from actor runtime startup to application startup.</summary>
public interface IActorRuntimeStartupSignal
{
    Task WaitAsync(CancellationToken cancellationToken);
}

public sealed class ActorRuntimeStartupSignal : IActorRuntimeStartupSignal
{
    readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync(CancellationToken cancellationToken)
        => completion.Task.WaitAsync(cancellationToken);

    public void Complete() => completion.TrySetResult();

    public void Fail(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        completion.TrySetException(exception);
    }
}
