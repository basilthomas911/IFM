using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum BrokerOrderObservationKind : byte
{
    Unknown = 0, Acknowledged = 1, Execution = 2, Commission = 3, Rejected = 4,
    Cancelled = 5, OrderCompleted = 6
}
