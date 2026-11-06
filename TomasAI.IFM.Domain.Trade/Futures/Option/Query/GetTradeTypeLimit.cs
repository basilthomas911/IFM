using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradeTypeLimit
{

    extension(GetTradeTypeLimitQuery query)
    {
        /// <summary>Reads the strategy-specific limit configuration for a legacy option trade.</summary>
        public async ValueTask<ServiceResult<TradeTypeLimitReadModel>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await context.DbFactory.GetTradeTypeLimitAsync(query.TradeId, query.TradeType, cancellationToken).ConfigureAwait(false) ?? new TradeTypeLimitReadModel();
            return new ServiceResult<TradeTypeLimitReadModel>(result);
        }
    }
}
