using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetOptionTrades
{

    extension(GetOptionTradesQuery query)
    {
        /// <summary>Reads the legacy option-trade projections for an order.</summary>
        public async ValueTask<ServiceResult<OptionTradeReadModel[]>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            OptionTradeReadModel[] result = [.. await context.DbFactory.TradeDb.GetOptionTradesAsync(query.OrderId, cancellationToken).ConfigureAwait(false)];
            return new ServiceResult<OptionTradeReadModel[]>(result);
        }
    }
}
