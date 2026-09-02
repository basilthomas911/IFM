using TomasAI.IFM.Domain.Application.Shared;

namespace TomasAI.IFM.Domain.Application.Actor.Event;

/// <summary>Thread-safe process-local lifecycle status used by health and late observers.</summary>
public sealed class ApplicationStartupStatusStore : IApplicationStartupStatusStore
{
    ApplicationStartupStatus _current = new()
    {
        State = ApplicationLifecycleState.Bootstrapped,
        Summary = "Application startup has not yet been requested."
    };

    public ApplicationStartupStatus Current => Volatile.Read(ref _current);

    public void Set(ApplicationStartupStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        Volatile.Write(ref _current, status);
    }
}
