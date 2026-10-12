using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

public interface IFinancialQueryMessage<out TRequest, TResult> : IQuery<FinancialRead<TResult>> where TResult : class
{
    int SchemaVersion { get; }
    LedgerPortfolioId QueryEntityId { get; }
    FinancialReadScope Scope { get; }
    TRequest Parameters { get; }
    Guid CorrelationId { get; }
    DateTime RequestedAtUtc { get; }
}
