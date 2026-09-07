using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Projector;

public sealed class TradeSelectionProjector(IDbContextFactory storage) : IFunctionProjector<TradeSelectionFunctionCompletedEvent>
{
    public async ValueTask ProjectAsync(TradeSelectionFunctionCompletedEvent completed, CancellationToken cancellationToken = default)
        => await storage.TradeDb.UpsertTradeSelectionAsync(completed, cancellationToken).ConfigureAwait(false);
}
