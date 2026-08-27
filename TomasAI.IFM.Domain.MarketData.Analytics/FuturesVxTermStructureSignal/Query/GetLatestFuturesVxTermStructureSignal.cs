using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query;

/// <summary>Handles latest projected VX term-structure queries.</summary>
public static class GetLatestFuturesVxTermStructureSignal
{
    /// <summary>Loads the latest valid projected curve for the requested date and configuration.</summary>
    public static ValueTask<FuturesVxTermStructureSignalReadModel?> ExecuteAsync(
        this GetLatestFuturesVxTermStructureSignalQuery query,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default) =>
        new(dbFactory.MarketDataDb.GetLatestFuturesVxTermStructureSignalAsync(
            query.ValueDate, query.ConfigurationId, cancellationToken));
}
