using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Application.Event.Actor;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.Application.Event;

/// <summary>Shutdown event-family placeholder; production shutdown ordering is deliberately deferred.</summary>
public static class ApplicationShutdown
{
    /// <summary>Gets the shared log source for this lifecycle family.</summary>
    public static string ServiceId { get; } = nameof(TomasAI.IFM.Shared.StatusConsole.LogSourceType.ApplicationShutdown);
    /// <summary>Processes a shutdown event using the current placeholder lifecycle behavior.</summary>
    public static async ValueTask ExecuteAsync(
        this ApplicationShutdownEvent @event,
        IApplicationEventContext context,
        ILogger<ApplicationEventActor> logger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation(
            "Application shutdown command {CommandId} was observed; production shutdown orchestration remains deferred.",
            @event.CommandId);
        try
        {
            await context.StatusConsoleWriter.WriteConsoleAsync(
                LogSourceType.System,
                "Application shutdown was requested, but production shutdown orchestration is deferred in Stage 1.")
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to publish deferred shutdown status to the System Console.");
        }
    }
}
