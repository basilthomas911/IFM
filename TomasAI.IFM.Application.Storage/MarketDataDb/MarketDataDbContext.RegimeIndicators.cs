using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    const string EmaConfigurationId = "ema-10-20-50-200-v1";
    const string BollingerBandConfigurationId = "bb-10-20-ema-center-population-v1";

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

    /// <inheritdoc />
    public Task<FuturesEmaSignalReadModel?> GetLatestFuturesEmaSignalAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly valueDate,
        CancellationToken cancellationToken = default) =>
        ReadLatestAsync(
            seriesIdentity,
            valueDate,
            EmaConfigurationId,
            MarketDataDbCql.GetLatestFuturesEmaSignal,
            nameof(MarketDataDbCql.GetLatestFuturesEmaSignal),
            MapToFuturesEmaSignal,
            cancellationToken);

    /// <inheritdoc />
    public Task<FuturesBbSignalReadModel?> GetLatestFuturesBollingerBandSignalAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly valueDate,
        CancellationToken cancellationToken = default) =>
        ReadLatestAsync(
            seriesIdentity,
            valueDate,
            BollingerBandConfigurationId,
            MarketDataDbCql.GetLatestFuturesBollingerBandSignal,
            nameof(MarketDataDbCql.GetLatestFuturesBollingerBandSignal),
            MapToFuturesBollingerBandSignal,
            cancellationToken);

    async Task<T?> ReadLatestAsync<T>(
        MarketSeriesIdentity seriesIdentity,
        DateOnly valueDate,
        string configurationId,
        string cql,
        string operation,
        Func<IObjectDataRecord, T> map,
        CancellationToken cancellationToken)
        where T : class
    {
        var endOfValueDateUtc = valueDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var result = await ReadMonthAsync(
            seriesIdentity, valueDate, endOfValueDateUtc, configurationId,
            cql, operation, map, cancellationToken).ConfigureAwait(false);
        if (result is not null)
            return result;

        var previousMonth = valueDate.AddMonths(-1);
        return await ReadMonthAsync(
            seriesIdentity, previousMonth, endOfValueDateUtc, configurationId,
            cql, operation, map, cancellationToken).ConfigureAwait(false);
    }

    Task<T?> ReadMonthAsync<T>(
        MarketSeriesIdentity seriesIdentity,
        DateOnly partitionMonth,
        DateTime marketDataAsOf,
        string configurationId,
        string cql,
        string operation,
        Func<IObjectDataRecord, T> map,
        CancellationToken cancellationToken)
        where T : class => _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{operation}", cql)
            .SetParameters(new GetLatestFuturesRegimeSignal(
                seriesIdentity.Format(), TimeFrameType.Daily.ToString(), configurationId,
                Bucket(partitionMonth), marketDataAsOf))
            .ExecuteSingleAsync(map, cancellationToken);

    static FuturesEmaSignalReadModel MapToFuturesEmaSignal(IObjectDataRecord value)
    {
        var metadata = MapMetadata(value, MarketAnalyticsSignalKind.Ema);
        return new()
        {
            Metadata = metadata,
            Price = value.GetDecimal(7),
            Ema10 = Decimal(value, 8),
            PreviousEma10 = Decimal(value, 9),
            Ema10Slope = Decimal(value, 10),
            Ema20 = Decimal(value, 11),
            PreviousEma20 = Decimal(value, 12),
            Ema20Slope = Decimal(value, 13),
            Ema50 = Decimal(value, 14),
            PreviousEma50 = Decimal(value, 15),
            Ema50Slope = Decimal(value, 16),
            Ema200 = Decimal(value, 17),
            PreviousEma200 = Decimal(value, 18),
            Ema200Slope = Decimal(value, 19),
            IsWarm = value.GetBool(20),
            BaselineValueDate = metadata.ValueDate
        };
    }

    static FuturesBbSignalReadModel MapToFuturesBollingerBandSignal(IObjectDataRecord value)
    {
        var metadata = MapMetadata(value, MarketAnalyticsSignalKind.BollingerBand, 23);
        return new()
        {
            Metadata = metadata,
            Price = value.GetDecimal(7),
            Ema10Center = Decimal(value, 8),
            StandardDeviation10 = Decimal(value, 9),
            Upper10 = Decimal(value, 10),
            Lower10 = Decimal(value, 11),
            Width10 = Decimal(value, 12),
            Position10 = Decimal(value, 13),
            Ema20Center = Decimal(value, 14),
            StandardDeviation20 = Decimal(value, 15),
            Upper20 = Decimal(value, 16),
            Lower20 = Decimal(value, 17),
            Width20 = Decimal(value, 18),
            Position20 = Decimal(value, 19),
            Width20Baseline = Decimal(value, 20),
            Width20Ratio = Decimal(value, 21),
            IsWarm = value.GetBool(22),
            BaselineValueDate = metadata.ValueDate
        };
    }

    static MarketAnalyticsSignalMetadata MapMetadata(
        IObjectDataRecord value,
        MarketAnalyticsSignalKind kind,
        int sourceSequenceIndex = 21)
    {
        var series = MarketSeriesIdentity.Parse(value.GetString(0));
        var timeFrame = value.GetEnum<TimeFrameType>(1);
        var calculatedAtIndex = sourceSequenceIndex + 1;
        var schemaVersionIndex = sourceSequenceIndex + 2;
        var calculationVersionIndex = sourceSequenceIndex + 3;
        var calculationMethodIndex = sourceSequenceIndex + 4;
        var isValidIndex = sourceSequenceIndex + 5;
        return new()
        {
            SignalKey = new(series, kind, timeFrame, value.GetString(2)),
            ContractId = value.GetString(5),
            ValueDate = value.GetDateOnly(6),
            ObservationId = new FuturesTradeSessionBarId(value.GetGuid(4)),
            MarketDataAsOfUtc = new DateTimeOffset(
                DateTime.SpecifyKind(value.GetDateTime(3), DateTimeKind.Utc)),
            CalculatedAtUtc = new DateTimeOffset(
                DateTime.SpecifyKind(value.GetDateTime(calculatedAtIndex), DateTimeKind.Utc)),
            SourceSequence = value.GetLong(sourceSequenceIndex),
            SchemaVersion = checked((ushort)value.GetInt(schemaVersionIndex)),
            CalculationVersion = value.GetString(calculationVersionIndex),
            CalculationMethod = value.GetEnum<MarketSignalCalculationMethod>(calculationMethodIndex),
            IsValid = value.GetBool(isValidIndex)
        };
    }

    static decimal? Decimal(IObjectDataRecord value, int index) =>
        value.IsNull(index) ? null : value.GetDecimal(index);

    static int Bucket(DateOnly valueDate) => (valueDate.Year * 100) + valueDate.Month;
}
