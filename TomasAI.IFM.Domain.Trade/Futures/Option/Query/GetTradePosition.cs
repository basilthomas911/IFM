using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradePosition
{

    extension(GetTradePositionQuery query)
    {
        /// <summary>Reads one legacy option position identified by its complete position key.</summary>
        public async ValueTask ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await context.DbFactory.GetTradePositionAsync(query.OrderId, query.TradeId, query.TradeType, query.ValueDate, query.DaysToExpiry, query.TradeStatus, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<TradePositionReadModel>(result)).ConfigureAwait(false);
        }
    }
}
