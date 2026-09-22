using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Events;
/// <summary>Defines non-serialized behavior shared by root Portfolio domain events.</summary>
public interface IPortfolioEvent : IEvent<ActorEntityId>
{
    long Revision { get; }
    DateTime OccurredOnUtc { get; }
    string Principal { get; }
    Guid CorrelationId { get; }
    Guid CausationId { get; }
    DateTime OriginatedOnUtc { get; }
}

/// <summary>Defines root Portfolio domain event behavior.</summary>
public interface IPortfolioDomainEvent : IPortfolioEvent { }
