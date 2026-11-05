using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Application.Event.Actor;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.Application.Event;

/// <summary>Reports the outcome of an application shutdown request.</summary>
public static class ApplicationShutdown
{
    /// <summary>Gets the shared log source for this lifecycle family.</summary>
    public static string ServiceId { get; } = nameof(TomasAI.IFM.Shared.StatusConsole.LogSourceType.ApplicationShutdown);

    extension(ApplicationShutdownEvent @event)
    {
        /// <summary>Reports that this host cannot orchestrate shutdown.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]
        public async ValueTask ExecuteAsync(
                IApplicationEventContext context,
                ILogger<ApplicationEventActor> logger, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();
            global::TomasAI.IFM.Domain.Application.ActorLifecycleDiagnosticLog.Write9(logger, null, @event.CommandId);
            var reason = new NotSupportedException("Application shutdown orchestration is unavailable on this host.");
            var failure = (ApplicationShutdownFailEvent)@event
                .ToFailEvent<ApplicationShutdownFailEvent, ApplicationEntityId>(reason);
            await context.SendAsync<ApplicationShutdownFailEvent, ApplicationEntityId>(failure)
                .ConfigureAwait(false);

            try
            {
                await context.StatusConsoleWriter.WriteConsoleAsync(
                    LogSourceType.System,
                    reason.Message)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                global::TomasAI.IFM.Domain.Application.ActorLifecycleDiagnosticLog.Write10(logger, exception);
            }
        }
    }
}
