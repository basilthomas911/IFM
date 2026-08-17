using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.StatusConsole.Events;

namespace TomasAI.IFM.Service.StatusConsole;

/// <summary>
/// Subscribes to NATS event streams and forwards <see cref="StatusConsoleLoggedEvent"/> domain events
/// to a caller-supplied action delegate, enabling real-time status console log display.
/// </summary>
/// <remarks>
/// Extends <see cref="NatsActorEventListener"/> and implements <see cref="IStatusConsoleEventConsumer"/>.
/// The consumer listens on the non-durable <c>Notify.StatusConsoleEvent</c> actor mailbox for the
/// <c>Logged</c> verb.
/// Only events that pass <see cref="StatusConsoleLoggedEvent.IsValid"/> are forwarded to the action.
/// </remarks>
/// <param name="options">NATS event listener configuration options (connection, retry, subject prefix, etc.).</param>
/// <param name="logger">Logger used by the underlying <see cref="NatsActorEventListener"/> for diagnostics.</param>
public class StatusConsoleEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IStatusConsoleEventConsumer
{
    Func<StatusConsoleLoggedEvent, ValueTask> _eventAction = default!;

    /// <summary>
    /// Starts the NATS subscription and begins forwarding <see cref="StatusConsoleLoggedEvent"/> events
    /// to the provided <paramref name="eventAction"/> callback.
    /// </summary>
    /// <remarks>
    /// Subscribes to the <c>Notify / StatusConsoleEvent / Logged</c> actor mailbox. Each received message is
    /// deserialised into a <see cref="StatusConsoleLoggedEvent"/> and passed to <paramref name="eventAction"/>
    /// only when <see cref="StatusConsoleLoggedEvent.IsValid"/> is <see langword="true"/>.
    /// </remarks>
    /// <param name="eventAction">Callback invoked for every valid <see cref="StatusConsoleLoggedEvent"/> received.</param>
    public async ValueTask StartAsync(Func<StatusConsoleLoggedEvent, ValueTask> eventAction)
    {
        _eventAction = eventAction;
        await StartAsync(
            "StatusConsoleEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Notify, StatusConsoleLoggedEvent.Actor)] = [StatusConsoleLoggedEvent.Verb]
            },
            EventHandlerAsync
        );

        /// local function to handle incoming events
        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            StatusConsoleLoggedEvent loggedEvent = eventVerb switch
            {
                _ when eventVerb == StatusConsoleLoggedEvent.Verb => eventMsg.AsEvent<StatusConsoleLoggedEvent>()!,
                _ => default!
            };
            if (loggedEvent.IsValid)
                await _eventAction(loggedEvent);
        }
    }

    /// <summary>
    /// Stops the NATS subscription and releases the event action callback.
    /// </summary>
    /// <remarks>
    /// Delegates to the base <see cref="NatsActorEventListener.StopAsync()"/> to cleanly unsubscribe
    /// from all NATS subjects before clearing the internal callback reference.
    /// </remarks>
    public new async ValueTask StopAsync()
    {
        await base.StopAsync();
        _eventAction = default!;
    }
}
