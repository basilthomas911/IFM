using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Projector;

public sealed class OrderCompositionProjector(IDbContextFactory storage) : IFunctionProjector<OrderCompositionFunctionCompletedEvent>
{
    public async ValueTask ProjectAsync(OrderCompositionFunctionCompletedEvent completed, CancellationToken cancellationToken = default)
        => await storage.TradeDb.UpsertOrderCompositionAsync(completed, cancellationToken).ConfigureAwait(false);
}
