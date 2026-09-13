using TomasAI.IFM.Domain.Trade.Futures.Option.Trade.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Trade;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Trade.Query.Extensions;

public static class FuturesOptionTradeQueryHandlers
{
    public static ValueTask ExecuteAsync(this GetIronCondorOptionTradeQuery query,
        IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken) =>
        ReplyOneAsync(query, query.TradeId, TradeStrategyKind.IronCondor, context, cancellationToken);

    public static ValueTask ExecuteAsync(this GetVerticalSpreadOptionTradeQuery query,
        IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken) =>
        ReplyOneAsync(query, query.TradeId, TradeStrategyKind.VerticalSpread, context, cancellationToken);

    public static ValueTask ExecuteAsync(this GetIronCondorOptionTradesQuery query,
        IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken) =>
        ReplyManyAsync(query, query.PortfolioId, query.FundId, TradeStrategyKind.IronCondor,
            query.FromUtc, query.ToUtc, query.PageSize, query.PagingState, context, cancellationToken);

    public static ValueTask ExecuteAsync(this GetVerticalSpreadOptionTradesQuery query,
        IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken) =>
        ReplyManyAsync(query, query.PortfolioId, query.FundId, TradeStrategyKind.VerticalSpread,
            query.FromUtc, query.ToUtc, query.PageSize, query.PagingState, context, cancellationToken);

    static async ValueTask ReplyOneAsync(
        IQuery query,
        TradeEntityId tradeId,
        TradeStrategyKind strategyKind,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        var value = await context.DbFactory.TradeDb
            .GetEstablishedTradeAsync(tradeId, cancellationToken)
            .ConfigureAwait(false);
        if (value?.StrategyKind != strategyKind) value = null;
        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<EstablishedTradeDefinition?>(value)).ConfigureAwait(false);
    }

    static async ValueTask ReplyManyAsync(
        IQuery query,
        int portfolioId,
        int fundId,
        TradeStrategyKind strategyKind,
        DateTime fromUtc,
        DateTime toUtc,
        int pageSize,
        byte[]? pagingState,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        var page = await context.DbFactory.TradeDb.GetEstablishedTradesAsync(
            portfolioId, fundId, strategyKind, fromUtc, toUtc, pageSize, pagingState, cancellationToken)
            .ConfigureAwait(false);
        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<EstablishedTradeDefinition[]>(page.Items)).ConfigureAwait(false);
    }
}
