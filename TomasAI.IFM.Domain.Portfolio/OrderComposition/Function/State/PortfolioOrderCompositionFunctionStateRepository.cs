using TomasAI.IFM.Application.Storage.PortfolioDb.OrderComposition;
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
    : ITransactionalFunctionStateRepository<PortfolioOrderCompositionFunctionState,EvaluatePortfolioOrderCompositionCommand,PortfolioOrderCompositionCompletedEvent>
{
    public async ValueTask<PortfolioOrderCompositionFunctionState> LoadStateAsync(EvaluatePortfolioOrderCompositionCommand request,CancellationToken token=default)
    {
        var state=new PortfolioOrderCompositionFunctionState {Id=request.Subject.ThreadId};
        var completed=await database.ReadOperationAsync<PortfolioOrderCompositionCompletedEvent>(request.PortfolioId,request.OperationId,null,token);
        if(completed is not null)state.ReplayEvents([completed]);
        return state;
    }
    public ValueTask SaveCompletedStateAsync(IFunctionActorContext context,PortfolioOrderCompositionFunctionState state,EvaluatePortfolioOrderCompositionCommand request,CancellationToken token=default)
        =>ValueTask.FromException(new InvalidOperationException("Portfolio order composition requires an enlisted business/event transaction."));
    public async ValueTask<PortfolioOrderCompositionCompletedEvent> CommitAsync(IFunctionActorContext context,EvaluatePortfolioOrderCompositionCommand request,PortfolioOrderCompositionCompletedEvent candidate,CancellationToken token=default)
        =>await store.EvaluateAsync(
            request,
            (command,book,revision,cancellationToken) => PortfolioOrderCompositionModel.EvaluateAsync(
                command,book,revision,identities,cancellationToken),
            token);
}
