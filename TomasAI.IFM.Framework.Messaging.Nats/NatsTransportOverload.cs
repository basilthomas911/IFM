using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>Settles rejected Core NATS deliveries without inspecting their payload type.</summary>
internal static class NatsTransportOverload
{
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
                        exception,
                        "Failed to reply to rejected Core NATS {ActorType} request; reason={Reason}.",
                        actorType,
                        reason.ToStringFast());
                }
                return;
            }

            if (trafficClass is CoreNatsTrafficClass.Optional or CoreNatsTrafficClass.DurableLiveCopy)
            {
                NatsMessagingMetrics.RecordOptionalDrop(actorType, trafficClass);
                logger.LogWarning(
                    "Dropped explicitly classified Core NATS {ActorType} traffic; class={TrafficClass}, reason={Reason}.",
                    actorType,
                    trafficClass,
                    reason.ToStringFast());
                return;
            }

            NatsMessagingMetrics.DispatchFailures.Add(1);
            logger.LogError(
                "Rejected Core NATS {ActorType} traffic without a reply subject; class={TrafficClass}, reason={Reason}. "
                + "Enforcement configuration must prevent this required or unknown traffic path.",
                actorType,
                trafficClass,
                reason.ToStringFast());
        }
        finally
        {
            message.Dispose();
        }
    }
}
