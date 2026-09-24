using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Application.Event.Actor;
using TomasAI.IFM.Domain.Application.Shared.Events;

namespace TomasAI.IFM.Domain.Application.Event;

/// <summary>Handles <see cref="ApplicationShutdownFailEvent"/> as a terminal Application lifecycle notification.</summary>
public static class ApplicationShutdownFail
{
    /// <summary>Gets the shared log source for this lifecycle family.</summary>
    public static string ServiceId { get; } = nameof(TomasAI.IFM.Shared.StatusConsole.LogSourceType.ApplicationShutdown);
    /// <summary>Acknowledges the terminal lifecycle event after honoring cancellation.</summary>
    public static ValueTask ExecuteAsync(this ApplicationShutdownFailEvent eventValue, IApplicationEventContext context, ILogger<ApplicationEventActor> logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventValue);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogError(
            "{ServiceId} failure event {EventId} for command {CommandId}, entity {EntityId}, code {ErrorCode}: {ErrorMessage}",
            ServiceId, eventValue.Id, eventValue.CommandId, eventValue.EntityId, eventValue.ErrorCode, eventValue.ErrorMessage);
        return ValueTask.CompletedTask;
    }
}
