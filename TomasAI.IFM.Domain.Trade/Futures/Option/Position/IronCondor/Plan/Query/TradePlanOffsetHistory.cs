using TomasAI.IFM.Domain.Trade.Model.Position.Query;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query;

/// <summary>Qualifies history requests and adds an optional chart at the exact page revision.</summary>
public static class TradePlanOffsetHistory
{
    /// <summary>Reads one page and its chart without reopening the PostgreSQL snapshot.</summary>
    public static Task<StrategyTradePlanHistoryPage> ReadAsync(ITradePlanDbContext database,
        StrategyPositionId position, TradeStrategyKind strategy, DateOnly date, int offset, int size,
        long? maximumRevision = null, CancellationToken token = default)
        => ReadCoreAsync(database, position, strategy, date, offset, size, maximumRevision, null, false, token);

    /// <summary>Reads anchored history with a continuation for the next sequential page when one exists.</summary>
    public static Task<StrategyTradePlanHistoryPage> ReadPageAsync(ITradePlanDbContext database,
        StrategyPositionId position, TradeStrategyKind strategy, DateOnly date, int offset, int size,
        long? maximumRevision = null, byte[]? pagingState = null, CancellationToken token = default)
        => ReadCoreAsync(database, position, strategy, date, offset, size, maximumRevision, pagingState, true, token);

    static async Task<StrategyTradePlanHistoryPage> ReadCoreAsync(ITradePlanDbContext database,
        StrategyPositionId position, TradeStrategyKind strategy, DateOnly date, int offset, int size,
        long? maximumRevision, byte[]? pagingState, bool returnContinuation, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (maximumRevision is < 0) throw new ArgumentOutOfRangeException(nameof(maximumRevision));
        TradePlanRequestQualification.RequirePageSize(size);
        TradePlanRequestQualification.RequireScope(position, strategy, date);

        await using var read = await (returnContinuation
            ? database.DbReader.OpenHistoryPageAsync(position, strategy, date, offset, size, maximumRevision, pagingState, token)
            : database.DbReader.OpenOffsetHistoryAsync(position, strategy, date, offset, size, maximumRevision, token))
            .ConfigureAwait(false);
        var page = read.Page;
        if (offset != 0 || page.ChartEndUtc is not { } end) return page;
        var series = await TradePlanChartCache.ReadAsync(
            (database.Database.ConnectionString, position, strategy, date, page.MaximumRevision), async () =>
            new TradePlanChartCache.Series(end, await read.ReadChartAsync(end.AddHours(-6), end, token).ConfigureAwait(false)), token)
            .ConfigureAwait(false);
        return page with { ChartEndUtc = series.EndUtc, ChartPoints = series.Points };
    }
}
