using TomasAI.IFM.Domain.Application.Shared;

namespace TomasAI.IFM.Domain.Application.Event;

/// <summary>Thread-safe process-local lifecycle status used by health and late observers.</summary>
public sealed class ApplicationStartupStatusStore : IApplicationStartupStatusStore
{
    ApplicationStartupStatus _current = new()
    {
        State = ApplicationLifecycleState.Bootstrapped,
        Summary = "Application startup has not yet been requested."
    };
    TaskCompletionSource<ApplicationStartupStatus> _changed = CreateChangeSignal();

    public ApplicationStartupStatus Current => Volatile.Read(ref _current);

    public void Set(ApplicationStartupStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        Volatile.Write(ref _current, status);
        Interlocked.Exchange(ref _changed, CreateChangeSignal()).TrySetResult(status);
    }

    public async ValueTask<ApplicationStartupStatus> WaitForChangeAsync(
        ApplicationStartupStatus observed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observed);
        while (true)
        {
            var current = Current;
            if (!ReferenceEquals(current, observed))
                return current;

            var changed = Volatile.Read(ref _changed).Task;
            current = Current;
            if (!ReferenceEquals(current, observed))
                return current;

            return await changed.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    static TaskCompletionSource<ApplicationStartupStatus> CreateChangeSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
