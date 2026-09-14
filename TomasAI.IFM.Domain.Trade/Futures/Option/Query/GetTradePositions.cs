using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradePositions
{
    /// <summary>Reads legacy position projections for an option trade.</summary>
    public static async ValueTask ExecuteAsync(
        this GetTradePositionsQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        TradePositionReadModel[] result = [.. await context.DbFactory.TradeDb
            .GetTradePositionsAsync(query.OrderId, query.TradeId, cancellationToken)
            .ConfigureAwait(false)];
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<TradePositionReadModel[]>(result)).ConfigureAwait(false);
    }
}
