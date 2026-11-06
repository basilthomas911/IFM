using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Query;
/// <summary>Shared storage operations used by strategy-specific Trade Plan query handlers.</summary>
public static class StrategyTradePlanQueryExecution
{
    /// <summary>Reads and returns the current material plan for one strategy position and value date.</summary>
    /// <param name = "dbFactory">The database-context factory.</param>
    /// <param name = "positionId">The strategy-position identity.</param>
    /// <param name = "strategy">The strategy that owns the plan.</param>
    /// <param name = "valueDate">The plan value date.</param>
    /// <param name = "cancellationToken">Cancels storage access.</param>
    /// <returns>The typed projected result.</returns>
    public static async ValueTask<ServiceResult<StrategyTradePlanSnapshot?>> ReadCurrentAsync(IDbContextFactory dbFactory, StrategyPositionId positionId, TradeStrategyKind strategy, DateOnly valueDate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        var result = await dbFactory.TradePlanDb.DbReader.GetCurrentAsync(positionId, strategy, valueDate, cancellationToken).ConfigureAwait(false);
        if (result is not null && strategy == TradeStrategyKind.IronCondor) {
            var trade = await dbFactory.TradeDb.GetEstablishedTradeAsync(positionId.Trade, cancellationToken).ConfigureAwait(false);
            if (trade is not null) {
                var position = result.Position;
                if (position.ContractCashMultiplier is null && result.IronCondorTradePlanSnapshot?.ContractMultiplier is > 0)
                    position = position with { ContractCashMultiplier = result.IronCondorTradePlanSnapshot.ContractMultiplier };
                result = result with { Position = StrategyPositionHistoryProjection.EnrichPosition(position, trade) };
            }
        }
        return new ServiceResult<StrategyTradePlanSnapshot?>(result);
    }

    /// <summary>Reads and returns one bounded page of material plan history.</summary>
    /// <param name = "dbFactory">The database-context factory.</param>
    /// <param name = "positionId">The strategy-position identity.</param>
    /// <param name = "strategy">The strategy that owns the plan.</param>
    /// <param name = "valueDate">The plan value date.</param>
    /// <param name = "pageSize">The maximum number of history rows.</param>
    /// <param name = "pagingState">The continuation state from the previous page.</param>
    /// <param name = "cancellationToken">Cancels storage access.</param>
    /// <returns>The typed projected result.</returns>
    public static async ValueTask<ServiceResult<StrategyTradePlanHistoryPage>> ReadHistoryAsync(IDbContextFactory dbFactory, StrategyPositionId positionId, TradeStrategyKind strategy, DateOnly valueDate, int pageSize, byte[]? pagingState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        var result = await dbFactory.TradePlanDb.DbReader.GetHistoryAsync(positionId, strategy, valueDate, pageSize, pagingState, cancellationToken).ConfigureAwait(false);
        StrategyTradePlanHistoryPage page = new(result.Items, result.PagingState);
        return new ServiceResult<StrategyTradePlanHistoryPage>(page);
    }

    /// <summary>Reads and returns one bounded value-date activity page across all strategy types.</summary>
    /// <param name = "dbFactory">The database-context factory.</param>
    /// <param name = "query">The activity query containing date and paging inputs.</param>
    /// <param name = "cancellationToken">Cancels storage access.</param>
    /// <returns>The typed projected result.</returns>
    public static async ValueTask<ServiceResult<StrategyTradePlanActivityPage>> ReadActivityAsync(IDbContextFactory dbFactory, GetStrategyTradePlanActivityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(query);
        var result = await dbFactory.TradePlanDb.DbReader.GetActivityAsync(query.ValueDate, query.PageSize, query.PagingState, cancellationToken).ConfigureAwait(false);
        StrategyTradePlanActivityPage page = new(result.Items, result.PagingState);
        return new ServiceResult<StrategyTradePlanActivityPage>(page);
    }
}
