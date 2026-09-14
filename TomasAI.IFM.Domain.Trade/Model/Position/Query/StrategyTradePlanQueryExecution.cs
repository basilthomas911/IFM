using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Query;

/// <summary>Shared storage operations used by strategy-specific Trade Plan query handlers.</summary>
public static class StrategyTradePlanQueryExecution
{
    /// <summary>Reads and replies with the current material plan for one strategy position and value date.</summary>
    /// <typeparam name="TActor">The query actor that owns the reply mailbox.</typeparam>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database-context factory.</param>
    /// <param name="query">The originating query.</param>
    /// <param name="positionId">The strategy-position identity.</param>
    /// <param name="strategy">The strategy that owns the plan.</param>
    /// <param name="valueDate">The plan value date.</param>
    /// <param name="cancellationToken">Cancels storage access.</param>
    /// <returns>A task that completes after the reply is sent.</returns>
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

    /// <summary>Reads and replies with one bounded page of material plan history.</summary>
    /// <typeparam name="TActor">The query actor that owns the reply mailbox.</typeparam>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database-context factory.</param>
    /// <param name="query">The originating query.</param>
    /// <param name="positionId">The strategy-position identity.</param>
    /// <param name="strategy">The strategy that owns the plan.</param>
    /// <param name="valueDate">The plan value date.</param>
    /// <param name="pageSize">The maximum number of history rows.</param>
    /// <param name="pagingState">The continuation state from the previous page.</param>
    /// <param name="cancellationToken">Cancels storage access.</param>
    /// <returns>A task that completes after the reply is sent.</returns>
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

    /// <summary>Reads and replies with one bounded value-date activity page across all strategy types.</summary>
    /// <typeparam name="TActor">The query actor that owns the reply mailbox.</typeparam>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database-context factory.</param>
    /// <param name="query">The activity query containing date and paging inputs.</param>
    /// <param name="cancellationToken">Cancels storage access.</param>
    /// <returns>A task that completes after the reply is sent.</returns>
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
