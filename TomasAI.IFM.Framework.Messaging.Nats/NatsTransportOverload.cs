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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The transport boundary records delivery failures or requests redelivery; one failed delivery or shutdown cleanup must not terminate unrelated messaging.")]
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
                    global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write19(logger, exception, nameof(NatsTransportOverload), nameof(SettleCoreRejectionAsync), message.Subject.ToString(), reason.ToStringFast());
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
            global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write20(logger, null, nameof(NatsTransportOverload), nameof(SettleCoreRejectionAsync), message.Subject.ToString(), trafficClass, reason.ToStringFast());
        }
        finally
        {
            message.Dispose();
        }
    }
}
