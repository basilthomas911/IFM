using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Indicators;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    /// <inheritdoc />
    public Task InsertFuturesEmaSignalAsync(
        FuturesEmaSignalReadModel signal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var metadata = signal.Metadata;
        return _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEmaSignalV1)}",
                MarketDataDbCql.InsertFuturesEmaSignalV1)
            .SetParameters(new InsertFuturesEmaSignalV1(
                metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(),
                metadata.CalculationConfigurationId, Bucket(metadata.ValueDate),
                metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value,
                metadata.ContractId, metadata.ValueDate, signal.Price,
                signal.Ema10, signal.PreviousEma10, signal.Ema10Slope,
                signal.Ema20, signal.PreviousEma20, signal.Ema20Slope,
                signal.Ema50, signal.PreviousEma50, signal.Ema50Slope,
                signal.Ema200, signal.PreviousEma200, signal.Ema200Slope, signal.IsWarm,
                metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime,
                metadata.SchemaVersion, metadata.CalculationVersion,
                metadata.CalculationMethod.ToString(), metadata.IsValid))
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task InsertFuturesBollingerBandSignalAsync(
        FuturesBollingerBandSignalReadModel signal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var metadata = signal.Metadata;
        return _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesBollingerBandSignalV1)}",
                MarketDataDbCql.InsertFuturesBollingerBandSignalV1)
            .SetParameters(new InsertFuturesBollingerBandSignalV1(
                metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(),
                metadata.CalculationConfigurationId, Bucket(metadata.ValueDate),
                metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value,
                metadata.ContractId, metadata.ValueDate, signal.Price,
                signal.Ema10Center, signal.StandardDeviation10, signal.Upper10, signal.Lower10,
                signal.Width10, signal.Position10, signal.Ema20Center, signal.StandardDeviation20,
                signal.Upper20, signal.Lower20, signal.Width20, signal.Position20,
                signal.Width20Baseline, signal.Width20Ratio, signal.IsWarm,
                metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime,
                metadata.SchemaVersion, metadata.CalculationVersion,
                metadata.CalculationMethod.ToString(), metadata.IsValid))
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task InsertFuturesAtrVolatilitySignalAsync(
        FuturesAtrVolatilitySignalReadModel signal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var metadata = signal.Metadata;
        return _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesAtrVolatilitySignalV1)}",
                MarketDataDbCql.InsertFuturesAtrVolatilitySignalV1)
            .SetParameters(new InsertFuturesAtrVolatilitySignalV1(
                metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(),
                metadata.CalculationConfigurationId, Bucket(metadata.ValueDate),
                metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value,
                metadata.ContractId, metadata.ValueDate, signal.TrueRange, signal.Atr14,
                signal.PreviousAtr14, signal.Atr14Baseline, signal.Atr14Ratio, signal.IsWarm,
                metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime,
                metadata.SchemaVersion, metadata.CalculationVersion,
                metadata.CalculationMethod.ToString(), metadata.IsValid))
            .ExecuteCommandAsync(cancellationToken);
    }

    static int Bucket(DateOnly valueDate) => (valueDate.Year * 100) + valueDate.Month;
}
