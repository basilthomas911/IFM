using TomasAI.IFM.Domain.Portfolio.OrderComposition.Repository;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State;

public sealed class PortfolioOrderCompositionFunctionStateRepository(
    IPortfolioDbReadContext database,
    PortfolioOrderCompositionStore store,
    IPortfolioBusinessIdAllocator identities)
    : ITransactionalFunctionStateRepository<PortfolioOrderCompositionFunctionState, EvaluatePortfolioOrderCompositionCommand, PortfolioOrderCompositionCompletedEvent>
{
    public async ValueTask<PortfolioOrderCompositionFunctionState> LoadStateAsync(EvaluatePortfolioOrderCompositionCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await global::TomasAI.IFM.Domain.Portfolio.Financial.Repository.PortfolioOperationCreationSnapshot.LoadAsync<PortfolioOrderCompositionFunctionState, PortfolioOrderCompositionCompletedEvent>(
            database, request.PortfolioId, request.OperationId, request.Subject.ThreadId, cancellationToken).ConfigureAwait(false);
    }
    public ValueTask SaveCompletedStateAsync(IFunctionActorContext context, PortfolioOrderCompositionFunctionState state, EvaluatePortfolioOrderCompositionCommand request, CancellationToken cancellationToken = default)
        => ValueTask.FromException(new InvalidOperationException("Portfolio order composition requires an enlisted business/event transaction."));
    public async ValueTask<PortfolioOrderCompositionCompletedEvent> CommitAsync(IFunctionActorContext context, EvaluatePortfolioOrderCompositionCommand request, PortfolioOrderCompositionCompletedEvent candidate, CancellationToken cancellationToken = default)
        => await (store.EvaluateAsync(
            request,
            (command, book, revision, financial, cancellationToken) => PortfolioOrderCompositionModel.EvaluateAsync(
                command, book, revision, financial, identities, cancellationToken),
            cancellationToken)).ConfigureAwait(false);
}
