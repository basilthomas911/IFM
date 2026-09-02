using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Application.Actor.Event.Actor;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.Application.Actor.Event;

/// <summary>Shutdown event-family placeholder; production shutdown ordering is deliberately deferred.</summary>
internal static class ApplicationShutdown
{
    public static async ValueTask ExecuteAsync(
        this ApplicationShutdownEvent @event,
        IApplicationEventContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.Logger.LogInformation(
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
            context.Logger.LogWarning(exception, "Unable to publish deferred shutdown status to the System Console.");
        }
    }
}
