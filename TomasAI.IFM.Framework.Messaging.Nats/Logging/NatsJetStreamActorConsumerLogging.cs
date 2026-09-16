using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>Defines allocation-efficient structured logs for durable actor-consumer recovery.</summary>
internal static partial class NatsJetStreamActorConsumerLogging
{
    [LoggerMessage(
        EventId = 42800,
        Level = LogLevel.Warning,
        Message = "Recreating invalid JetStream durable cursor. Stream={StreamName}; Consumer={ConsumerName}; RetainedMessages={RetainedMessages}; FirstSequence={FirstSequence}; LastSequence={LastSequence}; AcknowledgementFloor={AcknowledgementFloor}.")]
    internal static partial void RecoveringInvalidDurableCursor(
        ILogger logger,
        string streamName,
        string consumerName,
        ulong retainedMessages,
        ulong firstSequence,
        ulong lastSequence,
        ulong acknowledgementFloor);
}