using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Terminal execution reconciliation committed after all known financial facts are posted.</summary>
public interface ICapacityExecutionReconciledEvent : IEvent
{
    CapacityExecutionReconciliation CapacityReconciliation { get; }
}
