using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetOptionTrade
{

    extension(GetOptionTradeQuery query)
    {
        /// <summary>Reads one legacy option-trade projection by order and trade identifier.</summary>
        public async ValueTask<ServiceResult<OptionTradeReadModel?>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await context.DbFactory.TradeDb.GetOptionTradeAsync(query.OrderId, query.TradeId, cancellationToken).ConfigureAwait(false);
            return new ServiceResult<OptionTradeReadModel?>(result);
        }
    }
}
