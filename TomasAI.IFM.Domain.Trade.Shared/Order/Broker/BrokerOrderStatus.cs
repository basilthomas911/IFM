using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum BrokerOrderStatus : byte
{
    Unknown = 0, PlacePending = 1, Dispatched = 2, Working = 3, PartiallyFilled = 4,
    Filled = 5, CancelPending = 6, Cancelled = 7, Rejected = 8, OutcomeUnknown = 9,
    UpdatePending = 10
}
