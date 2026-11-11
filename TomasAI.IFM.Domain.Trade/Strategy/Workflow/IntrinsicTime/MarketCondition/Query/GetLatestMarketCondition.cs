using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query;
/// <summary>Handles <see cref = "GetLatestMarketConditionQuery"/>.</summary>
public static class GetLatestMarketCondition
{

    extension(GetLatestMarketConditionQuery query)
    {
        /// <summary>Executes the mapped Market Condition query and returns its typed result.</summary>
        public async ValueTask<ServiceResult<MarketConditionReadModel>> ExecuteAsync(IMarketConditionQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            MarketConditionHistorySelection.RequireFund(query.FundId, query.InstrumentRoot, 1);
            var values = await services.DbFactory.TradeDb.GetMarketConditionHistoryAsync(query.FundId, query.InstrumentRoot, query.TargetHorizon, DateTime.MaxValue, 1, cancellationToken).ConfigureAwait(false);
            var value = values.FirstOrDefault() ?? throw new KeyNotFoundException($"Latest Market Condition result for fund {query.FundId}/{query.InstrumentRoot}/{query.TargetHorizon} was not found.");
            return new ServiceResult<MarketConditionReadModel>(value);
        }
    }
}
