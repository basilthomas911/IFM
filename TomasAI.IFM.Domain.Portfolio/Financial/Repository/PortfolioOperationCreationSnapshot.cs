using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Restores an operation from its immutable first-and-only committed completion fact.</summary>
internal static class PortfolioOperationCreationSnapshot
{
    /// <summary>Keeps the canonical business/event transaction as the only source of operation state.</summary>
    internal static async ValueTask<TState> LoadAsync<TState, TCreationEvent>(
        IPortfolioDbReadContext database, int portfolioId, Guid operationId, ActorThreadId owner,
        CancellationToken cancellationToken)
        where TState : IEventSourceActorState<TState>, new()
        where TCreationEvent : class, IFinancialCompletedEvent, IEvent
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = new TState { Id = owner };
        var creation = await database.ReadOperationAsync<TCreationEvent>(
            portfolioId, operationId, null, cancellationToken).ConfigureAwait(false);
        if (creation is not null)
        {
            state.ReplayEvents(new IEvent[] { creation });
            state.RestoreCommittedStreamVersion(1);
        }
        return state;
    }
}
