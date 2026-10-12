using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum BrokerMutationKind : byte { Unknown = 0, Place = 1, UpdateLimit = 2, Cancel = 3 }
