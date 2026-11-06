using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradePositionTradeTypes
{

    extension(GetTradePositionTradeTypesQuery query)
    {
        /// <summary>Reads the legacy strategy types represented by an option position.</summary>
        public async ValueTask<ServiceResult<string[]>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            string[] result = [.. await context.DbFactory.TradeDb.GetTradePositionTradeTypesAsync(query.OrderId, query.TradeId, query.ValueDate, query.TradeStatus, query.DaysToExpiry, cancellationToken).ConfigureAwait(false)];
            return new ServiceResult<string[]>(result);
        }
    }
}
