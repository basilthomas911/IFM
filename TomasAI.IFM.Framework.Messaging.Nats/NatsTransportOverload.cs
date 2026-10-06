using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>Settles rejected Core NATS deliveries without inspecting their payload type.</summary>
internal static class NatsTransportOverload
{
    static readonly TomasAI.IFM.Shared.Telemetry.RepeatedLogGate<(ActorSubject, ActorAdmissionReason)> OptionalDropLogs = new(TimeSpan.FromSeconds(5));
    internal const string RetryableMessage =
        "Actor capacity is temporarily unavailable. Retry the request.";

    internal static ServiceResult<object> CreateReply(int errorCode)
        => new(errorCode, RetryableMessage);

    /// <summary>
    /// Replies or records the configured fire-and-forget disposition, then
    /// disposes the rejected message exactly once. This method intentionally
    /// absorbs reply failures because the Core subscription loop must continue.
    /// </summary>
    internal static async ValueTask SettleCoreRejectionAsync(
        IActorMessage message,
        ActorType actorType,
        ActorAdmissionReason reason,
        int errorCode,
        CoreNatsTrafficClass trafficClass,
        ILogger logger)
    {
        try
        {
            if (message.CanReply)
            {
                try
                {
                    await message.ReplyAsync(CreateReply(errorCode)).ConfigureAwait(false);
                    NatsMessagingMetrics.RecordOverloadReply(actorType, "succeeded");
                }
                catch (Exception exception)
                {
                    NatsMessagingMetrics.RecordOverloadReply(actorType, "failed");
                    NatsMessagingMetrics.DispatchFailures.Add(1);
                    logger.LogError(
                        exception,                        "{Component}.{Method} "+"Component=NatsTransportOverload Method=SettleCoreRejectionAsync Failed to reply to rejected Core NATS request for {Subject}; reason={Reason}.",nameof(NatsTransportOverload),nameof(SettleCoreRejectionAsync),                        message.Subject.ToString(),                        reason.ToStringFast());
                }
                return;
            }

            if (trafficClass == CoreNatsTrafficClass.Optional)
            {
                NatsMessagingMetrics.RecordOptionalDrop(actorType, trafficClass);
                if (logger.IsEnabled(LogLevel.Warning) && OptionalDropLogs.ShouldLog((message.Subject, reason), out var suppressedCount))
                    NatsTransportLogging.OptionalDrop(logger, message.Subject, trafficClass, reason, suppressedCount);
                return;
            }

            NatsMessagingMetrics.DispatchFailures.Add(1);
            logger.LogError(
                "{Component}.{Method} "+"Component=NatsTransportOverload Method=SettleCoreRejectionAsync Rejected Core NATS traffic for {Subject} without a reply subject; class={TrafficClass}, reason={Reason}. "
                + "Enforcement configuration must prevent this required or unknown traffic path.",nameof(NatsTransportOverload),nameof(SettleCoreRejectionAsync),                message.Subject.ToString(),                trafficClass,                reason.ToStringFast());
        }
        finally
        {
            message.Dispose();
        }
    }
}
