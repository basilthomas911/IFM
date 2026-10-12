using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

internal static partial class NatsAdditionalDiagnosticLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "NATS listener startup failed; Service={Service}; EventListenerId={EventListenerId}")]
    internal static partial void StartFailed(ILogger logger, Exception exception, string service, string eventListenerId);
    [LoggerMessage(Level = LogLevel.Error, Message = "NATS listener shutdown failed; Service={Service}; EventListenerId={EventListenerId}")]
    internal static partial void StopFailed(ILogger logger, Exception exception, string service, string eventListenerId);
    [LoggerMessage(Level = LogLevel.Debug, Message = "{Component}.{Method} NATS listener {EventListenerId} received event; Subject={Subject}")]
    internal static partial void ListenerReceived(ILogger logger, string component, string method, string eventListenerId, string subject);
    [LoggerMessage(Level = LogLevel.Debug, Message = "{Component}.{Method} NATS {ActorType} consumer received message; Subject={Subject}")]
    internal static partial void Received(ILogger logger, string component, string method, ActorType actorType, string subject);
    [LoggerMessage(Level = LogLevel.Error, Message = "{ProducerName}: actor type {ActorType} does not define a delivery transport.")]
    internal static partial void MissingTransport(ILogger logger, string producerName, ActorType actorType);
    [LoggerMessage(Level = LogLevel.Error, Message = "{Component}.{Method} settlement failed; Subject={Subject}; Action={Action}; Stream={Stream}; Consumer={Consumer}; StreamSequence={StreamSequence}; ConsumerSequence={ConsumerSequence}; DeliveryAttempt={DeliveryAttempt}; ElapsedMilliseconds={ElapsedMilliseconds}")]
    internal static partial void SettlementFailed(ILogger logger, Exception exception, string component, string method, string subject, string action, string? stream, string? consumer, object? streamSequence, object? consumerSequence, object? deliveryAttempt, double elapsedMilliseconds);
}
