using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioDb.OrderComposition;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State;

public sealed class PortfolioCloseOrderCompositionFunctionStateRepository(
    IPortfolioDbReadContext database,
    PortfolioCloseOrderCompositionStore store,
    IPortfolioBusinessIdAllocator identities) :
    ITransactionalFunctionStateRepository<PortfolioCloseOrderCompositionFunctionState,
        EvaluatePortfolioCloseOrderCompositionCommand, PortfolioCloseOrderCompositionCompletedEvent>
{
    public async ValueTask<PortfolioCloseOrderCompositionFunctionState> LoadStateAsync(
        EvaluatePortfolioCloseOrderCompositionCommand request, CancellationToken cancellationToken = default)
    {
        var state = new PortfolioCloseOrderCompositionFunctionState { Id = request.Subject.ThreadId };
        var completed = await database.ReadOperationAsync<PortfolioCloseOrderCompositionCompletedEvent>(
            request.PortfolioId, request.OperationId, null, cancellationToken).ConfigureAwait(false);
        if (completed is not null)
            state.ReplayEvents([completed]);
        return state;
    }

    public ValueTask SaveCompletedStateAsync(IFunctionActorContext context,
        PortfolioCloseOrderCompositionFunctionState state,
        EvaluatePortfolioCloseOrderCompositionCommand request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException(new InvalidOperationException(
            "Portfolio close-order composition requires an enlisted business/event transaction."));

    public ValueTask<PortfolioCloseOrderCompositionCompletedEvent> CommitAsync(
        IFunctionActorContext context,
        EvaluatePortfolioCloseOrderCompositionCommand request,
        PortfolioCloseOrderCompositionCompletedEvent candidate,
        CancellationToken cancellationToken = default) =>
        new(store.EvaluateAsync(request,
            (command, book, revision, openingOrder, token) =>
                PortfolioCloseOrderCompositionModel.EvaluateAsync(
                    command, book, revision, openingOrder, identities, token),
            cancellationToken));
}
