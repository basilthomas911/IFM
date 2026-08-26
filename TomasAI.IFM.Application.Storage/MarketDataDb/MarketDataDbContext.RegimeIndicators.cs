using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
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
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEmaSignal)}",
                MarketDataDbCql.InsertFuturesEmaSignal)
            .SetParameters(new InsertFuturesEmaSignal(
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
        FuturesBbSignalReadModel signal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var metadata = signal.Metadata;
        return _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesBollingerBandSignal)}",
                MarketDataDbCql.InsertFuturesBollingerBandSignal)
            .SetParameters(new InsertFuturesBollingerBandSignal(
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

    static int Bucket(DateOnly valueDate) => (valueDate.Year * 100) + valueDate.Month;
}
