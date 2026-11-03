using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Application.Event.Actor;
using TomasAI.IFM.Domain.Application.Shared.Events;

namespace TomasAI.IFM.Domain.Application.Event;

/// <summary>Handles <see cref="ApplicationStartupDegradedEvent"/> as a terminal Application lifecycle notification.</summary>
public static class ApplicationStartupDegraded
{
    /// <summary>Gets the shared log source for this lifecycle family.</summary>
    public static string ServiceId { get; } = nameof(TomasAI.IFM.Shared.StatusConsole.LogSourceType.ApplicationStartup);

    extension(ApplicationStartupDegradedEvent eventValue)
    {
        /// <summary>Acknowledges the terminal lifecycle event after honoring cancellation.</summary>
        public ValueTask ExecuteAsync(IApplicationEventContext context, ILogger<ApplicationEventActor> logger, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(eventValue);
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
}
