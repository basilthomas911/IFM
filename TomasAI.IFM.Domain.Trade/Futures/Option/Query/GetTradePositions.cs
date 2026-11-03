using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradePositions
{

    extension(GetTradePositionsQuery query)
    {
        /// <summary>Reads legacy position projections for an option trade.</summary>
        public async ValueTask ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            TradePositionReadModel[] result = [.. await context.DbFactory.TradeDb.GetTradePositionsAsync(query.OrderId, query.TradeId, cancellationToken).ConfigureAwait(false)];
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<TradePositionReadModel[]>(result)).ConfigureAwait(false);
        }
    }
}
