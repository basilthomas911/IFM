using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Model;

/// <summary>Provides shared projection reads for strategy-specific position queries.</summary>
internal static class PositionQueryModel
{
    /// <summary>Reads one position and filters it to the required strategy kind.</summary>
    internal static async ValueTask OneAsync(
        StrategyPositionQueryBase<StrategyPositionSnapshot> query,
        IFuturesOptionPositionQueryContext context,
        TradeStrategyKind strategyKind,
        CancellationToken cancellationToken)
    {
        var value = await context.DbFactory.TradeDb.GetStrategyPositionAsync(query.PositionId, cancellationToken);
        if (value?.StrategyKind != strategyKind)
            value = null;
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<StrategyPositionSnapshot?>(value));
    }

    /// <summary>Reads a position history page and filters it to the required strategy kind.</summary>
    internal static async ValueTask ManyAsync(
        PositionHistoryQuery query,
        IFuturesOptionPositionQueryContext context,
        TradeStrategyKind strategyKind,
        CancellationToken cancellationToken)
    {
        var page = await context.DbFactory.TradeDb.GetStrategyPositionHistoryAsync(
            query.PositionId.PositionId, query.FromUtc, query.ToUtc, query.PageSize, query.PagingState, cancellationToken);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<StrategyPositionSnapshot[]>(page.Items.Where(x => x.StrategyKind == strategyKind).ToArray()));
    }
}