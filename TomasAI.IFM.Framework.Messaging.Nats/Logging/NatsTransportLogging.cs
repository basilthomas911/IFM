using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;
internal static partial class NatsTransportLogging
{
    [LoggerMessage(42810, LogLevel.Warning, "{Component}.{Method} optional traffic dropped; Subject={Subject}; TrafficClass={TrafficClass}; Reason={Reason}; SuppressedCount={SuppressedCount}")]
    internal static partial void OptionalDrop(ILogger logger, ActorSubject subject, CoreNatsTrafficClass trafficClass, ActorAdmissionReason reason, long suppressedCount, string component = "NatsTransportOverload", string method = "SettleCoreRejectionAsync");
}
