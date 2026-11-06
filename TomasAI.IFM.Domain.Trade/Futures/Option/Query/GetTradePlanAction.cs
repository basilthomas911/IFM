using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradePlanAction
{

    extension(GetTradePlanActionQuery query)
    {
        /// <summary>Returns the empty compatibility result for the retired legacy trade-plan projection.</summary>
        public async ValueTask<ServiceResult<TradePlanActionReadModel[]>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            return new ServiceResult<TradePlanActionReadModel[]>([]);
        }
    }
}
