using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Projector;

public sealed class MarketConditionAssessmentProjector(IDbContextFactory storage) : IFunctionProjector<MarketConditionAssessmentCompletedEvent>
{
    public async ValueTask ProjectAsync(MarketConditionAssessmentCompletedEvent completed, CancellationToken cancellationToken = default)
        => await storage.TradeDb.UpsertMarketConditionAssessmentAsync(completed, cancellationToken).ConfigureAwait(false);
}
