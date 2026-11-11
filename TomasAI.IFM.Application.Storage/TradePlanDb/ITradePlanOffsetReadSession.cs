using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>Owns a repeatable PostgreSQL read snapshot until the caller disposes the session.</summary>
public interface ITradePlanOffsetReadSession : IAsyncDisposable
{
    /// <summary>The fixed history page, selected revision and chart end from this snapshot.</summary>
    StrategyTradePlanHistoryPage Page { get; }

    /// <summary>Reads price-only samples at this session's fixed revision.</summary>
    /// <param name="startUtc">The inclusive start of the chart.</param>
    /// <param name="endUtc">The inclusive end of the chart.</param>
    /// <param name="token">Cancels the read.</param>
    /// <returns>The selected minute samples in time order.</returns>
    Task<StrategyTradePlanChartPoint[]> ReadChartAsync(DateTime startUtc, DateTime endUtc, CancellationToken token);
}
