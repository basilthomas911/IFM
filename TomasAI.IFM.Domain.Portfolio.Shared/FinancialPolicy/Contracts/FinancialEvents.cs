using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventProjector;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

public interface IFinancialCompletedEvent : IEvent
{
    Guid OperationId { get; }
    int PortfolioId { get; }
    string InputHash { get; }
    DateTime CommittedAtUtc { get; }
}

