using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Application.Actor.Event.Actor;
using TomasAI.IFM.Domain.Application.Shared.Events;

namespace TomasAI.IFM.Domain.Application.Actor.Event;

/// <summary>Handles <see cref="ApplicationShutdownCompleteEvent"/> as a terminal Application lifecycle notification.</summary>
public static class ApplicationShutdownComplete
{
    /// <summary>Acknowledges the terminal lifecycle event after honoring cancellation.</summary>
    public static ValueTask ExecuteAsync(this ApplicationShutdownCompleteEvent eventValue, IApplicationEventContext context, ILogger<ApplicationEventActor> logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventValue);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
