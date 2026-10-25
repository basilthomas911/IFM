using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Implemented by the durable workflow acceptance event that precedes capacity consumption.</summary>
public interface ICapacityExecutionAcceptedEvent : IEvent
{
    CapacityExecutionAcceptance CapacityAcceptance { get; }
}
