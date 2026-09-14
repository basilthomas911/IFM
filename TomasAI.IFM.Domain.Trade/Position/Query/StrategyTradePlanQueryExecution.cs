using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Position.Query;

/// <summary>Shared storage operations used by strategy-specific Trade Plan query handlers.</summary>
public static class StrategyTradePlanQueryExecution
{
    /// <summary>Returns the current material plan for one strategy position and value date.</summary>
    public static async ValueTask ReplyCurrentAsync<TActor>(
        IQueryActorContext<TActor> context,
        IDbContextFactory dbFactory,
        IQuery query,
        StrategyPositionId positionId,
        TradeStrategyKind strategy,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        where TActor : IActor
    {
        var result = await dbFactory.TradePlanDb.DbReader.GetCurrentAsync(
            positionId, strategy, valueDate, cancellationToken).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<StrategyTradePlanSnapshot?>(result)).ConfigureAwait(false);
    }

    /// <summary>Returns one bounded page of material plan history.</summary>
    public static async ValueTask ReplyHistoryAsync<TActor>(
        IQueryActorContext<TActor> context,
        IDbContextFactory dbFactory,
        IQuery query,
        StrategyPositionId positionId,
        TradeStrategyKind strategy,
        DateOnly valueDate,
        int pageSize,
        byte[]? pagingState,
        CancellationToken cancellationToken)
        where TActor : IActor
    {
        var result = await dbFactory.TradePlanDb.DbReader.GetHistoryAsync(
            positionId, strategy, valueDate, pageSize, pagingState, cancellationToken).ConfigureAwait(false);
        StrategyTradePlanHistoryPage page = new(result.Items, result.PagingState);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<StrategyTradePlanHistoryPage>(page)).ConfigureAwait(false);
    }

    /// <summary>Returns one bounded value-date activity page across all strategy types.</summary>
    public static async ValueTask ReplyActivityAsync<TActor>(
        IQueryActorContext<TActor> context,
        IDbContextFactory dbFactory,
        GetStrategyTradePlanActivityQuery query,
        CancellationToken cancellationToken)
        where TActor : IActor
    {
        var result = await dbFactory.TradePlanDb.DbReader.GetActivityAsync(
            query.ValueDate, query.PageSize, query.PagingState, cancellationToken).ConfigureAwait(false);
        StrategyTradePlanActivityPage page = new(result.Items, result.PagingState);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<StrategyTradePlanActivityPage>(page)).ConfigureAwait(false);
    }
}
