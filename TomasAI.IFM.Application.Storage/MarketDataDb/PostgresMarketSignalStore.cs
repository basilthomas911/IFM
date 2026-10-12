using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Materializes accepted analytic values without recalculation, cache publication or provider calls.</summary>
public static class PostgresMarketSignalStore
{
    /// <summary>Returns false for control/notification events that have no signal table projection.</summary>
    public static async Task<bool> TryProjectAsync(IEnlistedPostgresTransaction transaction, IEvent source, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(source);
        var projection = source switch
        {
            TradeLiveFeedTurnedOnEvent accepted => WriteLiveFeedAsync(transaction,accepted,token),
            TradeLiveFeedTurnedOffEvent accepted => DeleteLiveFeedAsync(transaction,accepted,token),
            FuturesEodDataInsertedEvent accepted => WriteRawEodAsync(transaction,accepted,token),
            VixFuturesEodDataInsertedEvent accepted => WriteVixAsync(transaction,accepted,token),
            FuturesBarDataInsertedEvent accepted => WriteBarAsync(transaction,accepted,token),
            FuturesBarDataDeletedEvent accepted => DeleteBarAsync(transaction,accepted,token),
            FuturesTradeSessionBarPublishedEvent accepted => WriteSessionBarAsync(transaction,accepted.Bar,token),
            FuturesClosingPriceInsertedEvent accepted => WriteClosingAsync(transaction,accepted,token),
            MarketOutlookSnapshotInsertedEvent accepted => WriteOutlookAsync(transaction,accepted,source.EventId,token),
            FuturesAdxSignalGeneratedEvent accepted => WriteAdxAsync(transaction,accepted.FuturesAdxSignal,token),
            FuturesAdxDailySignalGeneratedEvent accepted => WriteAdxAsync(transaction,accepted.FuturesAdxSignal,token),
            FuturesAtrSignalGeneratedEvent accepted => WriteAtrAsync(transaction,accepted.FuturesAtrSignal,token),
            FuturesAtrDailySignalGeneratedEvent accepted => WriteAtrAsync(transaction,accepted.FuturesAtrSignal,token),
            FuturesMacdSignalGeneratedEvent accepted => WriteMacdAsync(transaction,accepted.FuturesMacdSignal,token),
            FuturesMacdDailySignalGeneratedEvent accepted => WriteMacdAsync(transaction,accepted.FuturesMacdSignal,token),
            FuturesRsiSignalGeneratedEvent accepted => WriteRsiAsync(transaction,accepted.FuturesRsiSignal,token),
            FuturesRsiDailySignalGeneratedEvent accepted => WriteRsiAsync(transaction,accepted.FuturesRsiSignal,token),
            FuturesTdiSignalGeneratedEvent accepted => WriteTdiAsync(transaction,accepted.FuturesTdiSignal,token),
            FuturesEmaSignalGeneratedEvent accepted => WriteEmaAsync(transaction,accepted.FuturesEmaSignal,token),
            FuturesBbSignalGeneratedEvent accepted => WriteBbAsync(transaction,accepted.FuturesBbSignal,token),
            FuturesVwapSignalUpdatedEvent accepted => WriteVwapAsync(transaction,accepted.FuturesVwapSignal,token),
            FuturesVxTermStructureSignalUpdatedEvent accepted => WriteVxTermStructureAsync(transaction,accepted.FuturesVxTermStructureSignal,token),
            FuturesTradeSignalUpdatedEvent accepted => WriteTradeAsync(transaction,accepted.FuturesTradeSignal,source.EventId,token),
            FuturesItiSignalGeneratedEvent accepted => WriteItiAsync(transaction,accepted.FuturesItiSignal,source.EventId,token),
            FuturesItiSignalHoldTradeSetEvent accepted => WriteItiAsync(transaction,accepted.FuturesItiSignal,source.EventId,token),
            FuturesItiSignalHoldTradeClearedEvent accepted => WriteItiAsync(transaction,accepted.FuturesItiSignal,source.EventId,token),
            _ => null
        };
        if (projection is null) return false;
        await projection.ConfigureAwait(false);
        return true;
    }

    static async Task WriteLiveFeedAsync(IEnlistedPostgresTransaction transaction,TradeLiveFeedTurnedOnEvent source,CancellationToken token)
    {
        var parameters = new InsertTradeLiveFeed(source.OrderId,source.TradeId,TradeLiveFeedStateType.On.ToStringFast()) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertTradeLiveFeed,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task DeleteLiveFeedAsync(IEnlistedPostgresTransaction transaction,TradeLiveFeedTurnedOffEvent source,CancellationToken token)
    {
        var parameters = new DeleteTradeLiveFeed(source.OrderId,source.TradeId) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.DeleteTradeLiveFeed,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    /// <summary>Captures resolved VX replacement facts before appending the source event in the same transaction.</summary>
    public static async Task<VixFuturesEodDataInsertedEvent> AcceptVixAsync(IEnlistedPostgresTransaction transaction,
        VixFuturesEodDataInsertedEvent source,CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(source);
        return source with { AcceptedSnapshot = await PostgresVixObservationExtensions.ResolveSnapshotAsync(
            transaction,source.VixFuturesTickData,source.SessionStatistics,token)
            .ConfigureAwait(false) };
    }

    static async Task WriteRawEodAsync(IEnlistedPostgresTransaction transaction,FuturesEodDataInsertedEvent source,CancellationToken token)
    {
        var value = source.RawObservation ?? throw new InvalidOperationException("This older EOD source lacks accepted session facts; use a verified baseline.");
        var parameters = new HistoricalRawEodParameter(value) { PostgreSql = true };
        _ = await transaction.ScalarAsync(MarketDataDbSql.TryInsertRawEod,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteVixAsync(IEnlistedPostgresTransaction transaction,VixFuturesEodDataInsertedEvent source,CancellationToken token)
    {
        var value = source.AcceptedSnapshot ?? throw new InvalidOperationException("This older VX source lacks accepted replacement facts; use a verified baseline.");
        var parameters = new InsertVixFuturesEodData(value.ContractId,value.ValueDate,value.OpenPrice,value.HighPrice,value.LowPrice,value.ClosePrice,value.Volume) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertVixFuturesEodData,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteBarAsync(IEnlistedPostgresTransaction transaction,FuturesBarDataInsertedEvent source,CancellationToken token)
    {
        var value = source.FuturesBarData;
        var parameters = new InsertFuturesBarData(value.ContractId,value.Symbol,value.ValueDate,value.BarDate,value.BarRateType.ToStringFast(),value.BarValue,value.UpTrendTrigger,value.DownTrendTrigger) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesBarData,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task DeleteBarAsync(IEnlistedPostgresTransaction transaction,FuturesBarDataDeletedEvent source,CancellationToken token)
    {
        var value = source.BarDataId;
        var parameters = new DeleteFuturesBarData(value.ContractId,value.Symbol,value.ValueDate) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.DeleteFuturesBarData,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteSessionBarAsync(IEnlistedPostgresTransaction transaction, FuturesTradeSessionBarReadModel bar, CancellationToken token)
    {
        var parameters = new HistoricalObservationParameter(bar) { PostgreSql = true };
        _ = await transaction.ScalarAsync(MarketDataDbSql.TryInsertObservation,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
        if (bar.TimeFrame != TimeFrameType.Daily) return;
        // The accumulator fixes daily interval bounds to the complete session before accepting this event.
        var eod = new FuturesEodObservationReadModel
        {
            MarketSeriesIdentity=bar.MarketSeriesIdentity,ContractId=bar.ContractId,ValueDate=bar.ValueDate,
            SessionStartUtc=bar.IntervalStartUtc,SessionEndUtc=bar.IntervalEndUtc,
            Open=bar.Open,High=bar.High,Low=bar.Low,Close=bar.Close,Volume=bar.Volume,
            TradeCount=bar.TradeCount,PriceVolumeSum=bar.PriceVolumeSum,ObservationId=bar.ObservationId,
            FirstSourceSequence=bar.FirstSourceSequence,LastSourceSequence=bar.LastSourceSequence,
            FirstMarketEventUtc=bar.FirstMarketEventUtc,LastMarketEventUtc=bar.LastMarketEventUtc,
            SchemaVersion=bar.SchemaVersion,IsComplete=bar.IsComplete,IsValid=bar.IsValid
        };
        var raw = new HistoricalRawEodParameter(eod) { PostgreSql = true };
        _ = await transaction.ScalarAsync(MarketDataDbSql.TryInsertRawEod,(Npgsql.NpgsqlParameter[])raw.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteClosingAsync(IEnlistedPostgresTransaction transaction, FuturesClosingPriceInsertedEvent accepted, CancellationToken token)
    {
        var closing = new InsertFuturesClosingPrice(accepted.FuturesClosingPriceId.ContractId, accepted.FuturesClosingPriceId.ValueDate,
            accepted.ClosingPrice, accepted.CreatedOn, accepted.CreatedBy) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesClosingPrice,(Npgsql.NpgsqlParameter[])closing.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteOutlookAsync(IEnlistedPostgresTransaction transaction, MarketOutlookSnapshotInsertedEvent accepted, long eventId, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
        var snapshot = accepted.MarketOutlook;
        var parameters = new UpsertMarketOutlookSnapshot(snapshot.ContractId, snapshot.ValueDate, eventId, snapshot.UpdatedAtUtc,
            MessagePackSerializer.Serialize(snapshot.FuturesEodData,MarketDataDbContext.MarketOutlookSerializerOptions,cancellationToken:token),
            snapshot.FuturesTradeSignal is null ? null : MessagePackSerializer.Serialize(snapshot.FuturesTradeSignal,MarketDataDbContext.MarketOutlookSerializerOptions,cancellationToken:token),
            snapshot.MissingInputs,MessagePackSerializer.Serialize(snapshot,MarketDataDbContext.MarketOutlookSerializerOptions,cancellationToken:token)) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.UpsertMarketOutlookSnapshot,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteAdxAsync(IEnlistedPostgresTransaction transaction, FuturesAdxSignalReadModel futuresAdxSignal, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(futuresAdxSignal);
        var parameters = new InsertFuturesAdxSignal(contractId: futuresAdxSignal.ContractId, valueDate: futuresAdxSignal.ValueDate, timePeriod: futuresAdxSignal.TimePeriod.ToStringFast(), periodLength: futuresAdxSignal.PeriodLength, timestamp: futuresAdxSignal.Timestamp, futuresPrice: futuresAdxSignal.FuturesPrice, plusDI: futuresAdxSignal.PlusDI, minusDI: futuresAdxSignal.MinusDI, adxValue: futuresAdxSignal.AdxValue, adx: futuresAdxSignal.ADX.ToStringFast(), adxStrength: futuresAdxSignal.ADXStrength.ToStringFast(), configurationId: futuresAdxSignal.Metadata?.CalculationConfigurationId, observationId: futuresAdxSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresAdxSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresAdxSignal.Metadata?.SourceSequence, calculationVersion: futuresAdxSignal.Metadata?.CalculationVersion, calculationMethod: futuresAdxSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresAdxSignal.Metadata is { } metadata ? metadata.SchemaVersion : null, isValid: futuresAdxSignal.Metadata?.IsValid, isWarm: futuresAdxSignal.IsWarm) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesAdxSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteAtrAsync(IEnlistedPostgresTransaction transaction, FuturesAtrSignalReadModel futuresAtrSignal, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(futuresAtrSignal);
        var parameters = new InsertFuturesAtrSignal(contractId: futuresAtrSignal.ContractId, valueDate: futuresAtrSignal.ValueDate, timePeriod: futuresAtrSignal.TimePeriod.ToStringFast(), periodLength: futuresAtrSignal.PeriodLength, timestamp: futuresAtrSignal.Timestamp, futuresPrice: futuresAtrSignal.FuturesPrice, atrValue: futuresAtrSignal.AtrValue, trueRange: futuresAtrSignal.TrueRange, atr: futuresAtrSignal.ATR.ToStringFast(), atrStrength: futuresAtrSignal.ATRStrength.ToStringFast(), configurationId: futuresAtrSignal.Metadata?.CalculationConfigurationId, observationId: futuresAtrSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresAtrSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresAtrSignal.Metadata?.SourceSequence, calculationVersion: futuresAtrSignal.Metadata?.CalculationVersion, calculationMethod: futuresAtrSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresAtrSignal.Metadata is { } atrMetadata ? atrMetadata.SchemaVersion : null, isValid: futuresAtrSignal.Metadata?.IsValid, previousAtrValue: futuresAtrSignal.PreviousAtrValue, atrBaseline: futuresAtrSignal.AtrBaseline, atrRatio: futuresAtrSignal.AtrRatio, isWarm: futuresAtrSignal.IsWarm) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesAtrSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteMacdAsync(IEnlistedPostgresTransaction transaction, FuturesMacdSignalReadModel futuresMacdSignal, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(futuresMacdSignal);
        var parameters = new InsertFuturesMacdSignal(contractId: futuresMacdSignal.ContractId, valueDate: futuresMacdSignal.ValueDate, timePeriod: futuresMacdSignal.TimePeriod.ToStringFast(), signalEmaPeriod: futuresMacdSignal.SignalEmaPeriod, fastEmaPeriod: futuresMacdSignal.FastEmaPeriod, slowEmaPeriod: futuresMacdSignal.SlowEmaPeriod, timestamp: futuresMacdSignal.Timestamp, futuresPrice: futuresMacdSignal.FuturesPrice, fastEma: futuresMacdSignal.FastEma, slowEma: futuresMacdSignal.SlowEma, macdLine: futuresMacdSignal.MacdLine, signalLine: futuresMacdSignal.SignalLine, histogram: futuresMacdSignal.Histogram, macd: futuresMacdSignal.MACD.ToStringFast(), macdStrength: futuresMacdSignal.MACDStrength.ToStringFast(), configurationId: futuresMacdSignal.Metadata?.CalculationConfigurationId, observationId: futuresMacdSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresMacdSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresMacdSignal.Metadata?.SourceSequence, calculationVersion: futuresMacdSignal.Metadata?.CalculationVersion, calculationMethod: futuresMacdSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresMacdSignal.Metadata is { } macdMetadata ? macdMetadata.SchemaVersion : null, isValid: futuresMacdSignal.Metadata?.IsValid, isWarm: futuresMacdSignal.IsWarm, observationCount: futuresMacdSignal.ObservationCount) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesMacdSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteRsiAsync(IEnlistedPostgresTransaction transaction, FuturesRsiSignalReadModel e, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(e);
        var parameters = new InsertFuturesRsiSignal(e.ContractId, e.ValueDate, e.TimePeriod.ToStringFast(), e.PeriodLength, e.Timestamp, e.Price, e.PriceChange, e.PriceGain, e.PriceLoss, e.AveragePriceGain, e.AveragePriceLoss, e.RS, e.RSI, e.RSIAverage, e.RSISlope, e.SourceSequence, e.SourceEventTimestamp, e.Metadata?.CalculationConfigurationId, e.Metadata?.ObservationId.Value, e.Metadata?.MarketDataAsOfUtc.UtcDateTime, e.Metadata?.CalculationVersion, e.Metadata?.CalculationMethod.ToString(), e.Metadata is { } rsiMetadata ? rsiMetadata.SchemaVersion : null, e.Metadata?.IsValid, e.PreviousRsi, e.RegimeSlope, e.IsWarm) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesRsiSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteTdiAsync(IEnlistedPostgresTransaction transaction, FuturesTdiSignalReadModel futuresTdiSignal, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(futuresTdiSignal);
        var parameters = new InsertFuturesTdiSignal(contractId: futuresTdiSignal.ContractId, timePeriod: futuresTdiSignal.TimePeriod.ToStringFast(), configurationId: futuresTdiSignal.ConfigurationId, valueDate: futuresTdiSignal.ValueDate, timestamp: futuresTdiSignal.Timestamp, schemaVersion: futuresTdiSignal.SchemaVersion, rsiPeriod: futuresTdiSignal.RsiPeriod, priceLinePeriod: futuresTdiSignal.PriceLinePeriod, signalLinePeriod: futuresTdiSignal.SignalLinePeriod, marketBasePeriod: futuresTdiSignal.MarketBasePeriod, volatilityBandPeriod: futuresTdiSignal.VolatilityBandPeriod, volatilityBandDeviation: futuresTdiSignal.VolatilityBandDeviation, price: futuresTdiSignal.Price, rsi: futuresTdiSignal.Rsi, priceLine: futuresTdiSignal.PriceLine, signalLine: futuresTdiSignal.SignalLine, marketBaseLine: futuresTdiSignal.MarketBaseLine, upperVolatilityBand: futuresTdiSignal.UpperVolatilityBand, lowerVolatilityBand: futuresTdiSignal.LowerVolatilityBand, bandWidth: futuresTdiSignal.BandWidth, priceSignalDivergence: futuresTdiSignal.PriceSignalDivergence, crossType: futuresTdiSignal.Cross.ToString(), marketState: futuresTdiSignal.MarketState.ToString(), trendDirection: futuresTdiSignal.TDI.ToStringFast(), trendStrength: futuresTdiSignal.TDIStrength.ToStringFast(), sourceSequence: futuresTdiSignal.SourceSequence, sourceEventTimestamp: futuresTdiSignal.SourceEventTimestamp) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTdiSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteEmaAsync(IEnlistedPostgresTransaction transaction, FuturesEmaSignalReadModel signal, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var metadata = signal.Metadata;
        var parameters = new InsertFuturesEmaSignal(metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(), metadata.CalculationConfigurationId, MarketDataDbContextExtensions.Bucket(metadata.ValueDate), metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value, metadata.ContractId, metadata.ValueDate, signal.Price, signal.Ema10, signal.PreviousEma10, signal.Ema10Slope, signal.Ema20, signal.PreviousEma20, signal.Ema20Slope, signal.Ema50, signal.PreviousEma50, signal.Ema50Slope, signal.Ema200, signal.PreviousEma200, signal.Ema200Slope, signal.IsWarm, metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime, metadata.SchemaVersion, metadata.CalculationVersion, metadata.CalculationMethod.ToString(), metadata.IsValid) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesEmaSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteBbAsync(IEnlistedPostgresTransaction transaction, FuturesBbSignalReadModel signal, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var metadata = signal.Metadata;
        var parameters = new InsertFuturesBollingerBandSignal(metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(), metadata.CalculationConfigurationId, MarketDataDbContextExtensions.Bucket(metadata.ValueDate), metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value, metadata.ContractId, metadata.ValueDate, signal.Price, signal.Ema10Center, signal.StandardDeviation10, signal.Upper10, signal.Lower10, signal.Width10, signal.Position10, signal.Ema20Center, signal.StandardDeviation20, signal.Upper20, signal.Lower20, signal.Width20, signal.Position20, signal.Width20Baseline, signal.Width20Ratio, signal.IsWarm, metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime, metadata.SchemaVersion, metadata.CalculationVersion, metadata.CalculationMethod.ToString(), metadata.IsValid) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesBollingerBandSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteVwapAsync(IEnlistedPostgresTransaction transaction, FuturesVwapSignalReadModel signal, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var parameters = new InsertFuturesVwapSignal(signal.ContractId, signal.ValueDate, signal.ConfigurationId, signal.AsOfUtc.UtcDateTime, signal.LastTradeOrdinal, signal.SessionStartUtc.UtcDateTime, signal.SessionEndUtc.UtcDateTime, signal.CumulativePriceVolume, signal.CumulativeVolume, signal.EligibleTradeCount, signal.RejectedTradeCount, signal.LastPrice, signal.Vwap, signal.PriceMinusVwap, signal.PriceToVwapPercent, signal.LastTradeSourceSequence, signal.StreamEpochId, signal.IsWarm, signal.IsValid, signal.InvalidReason.ToString(), signal.IsTickExact, signal.CalculationMethod.ToString(), signal.SchemaVersion, signal.CalculationVersion) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesVwapSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteVxTermStructureAsync(IEnlistedPostgresTransaction transaction, FuturesVxTermStructureSignalReadModel signal, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var parameters = new InsertFuturesVxTermStructureSignal(signal.ValueDate, signal.ConfigurationId, signal.CalculatedAtUtc.UtcDateTime, signal.FrontSourceSequence, signal.BackSourceSequence, signal.FrontVxContractId, signal.FrontExpiry, signal.FrontVxPrice, signal.BackVxContractId, signal.BackExpiry, signal.BackVxPrice, signal.FrontBackSpread, signal.FrontBackRatio, signal.TermStructurePercent, signal.TermStructureState.ToString(), signal.PriorFrontBackRatio, signal.PriorTermStructurePercent, signal.FrontSourceTimestampUtc.UtcDateTime, signal.BackSourceTimestampUtc.UtcDateTime, signal.IsWarm, signal.IsValid, signal.SchemaVersion, signal.CalculationVersion) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesVxTermStructureSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
            .ConfigureAwait(false);
    }
    static async Task WriteTradeAsync(IEnlistedPostgresTransaction transaction, FuturesTradeSignalV2ReadModel FuturesTradeSignalV2ReadModel, long eventId, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(FuturesTradeSignalV2ReadModel);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
        var sequenceId = eventId;
        var timePeriod = FuturesTradeSignalV2ReadModel.TimePeriod.ToStringFast();
        var parameters0 = new InsertFuturesTradeSignal(contractId: FuturesTradeSignalV2ReadModel.ContractId, valueDate: FuturesTradeSignalV2ReadModel.ValueDate, timePeriod, sequenceId, timestamp: FuturesTradeSignalV2ReadModel.Timestamp, mean: FuturesTradeSignalV2ReadModel.Mean, stdDev: FuturesTradeSignalV2ReadModel.StdDev, futuresPrice: FuturesTradeSignalV2ReadModel.FuturesPrice, priceChangePercent: FuturesTradeSignalV2ReadModel.PriceChangePercent, fundRiskPercent: FuturesTradeSignalV2ReadModel.FundRiskPercent, rsi: FuturesTradeSignalV2ReadModel.RSI, rsiSlope: FuturesTradeSignalV2ReadModel.RSISlope, trendType: FuturesTradeSignalV2ReadModel.TrendType.ToStringFast(), trendStrength: FuturesTradeSignalV2ReadModel.TrendStrength.ToStringFast(), tradeSignal: FuturesTradeSignalV2ReadModel.TradeSignal.ToStringFast(), tdi: FuturesTradeSignalV2ReadModel.TDI.ToStringFast(), tdiStrength: FuturesTradeSignalV2ReadModel.TDIStrength.ToStringFast(), mdi: FuturesTradeSignalV2ReadModel.MDI, mdiTrend: FuturesTradeSignalV2ReadModel.MDITrend.ToStringFast(), mdiUpTrendLimit: FuturesTradeSignalV2ReadModel.MDIUpTrendLimit, mdiDownTrendLimit: FuturesTradeSignalV2ReadModel.MDIDownTrendLimit, upTrendingTrigger: FuturesTradeSignalV2ReadModel.UpTrendingTrigger, downTrendingTrigger: FuturesTradeSignalV2ReadModel.DownTrendingTrigger, entryTrigger: FuturesTradeSignalV2ReadModel.EntryTrigger, exitTrigger: FuturesTradeSignalV2ReadModel.ExitTrigger, trendDelta: FuturesTradeSignalV2ReadModel.TrendDelta, trendExtreme: FuturesTradeSignalV2ReadModel.TrendExtreme, trendReversal: FuturesTradeSignalV2ReadModel.TrendReversal, fiftyDma: FuturesTradeSignalV2ReadModel.FiftyDMA, twoHundredDma: FuturesTradeSignalV2ReadModel.TwoHundredDMA, tradeExecuteState: FuturesTradeSignalV2ReadModel.TradeExecuteState.ToStringFast()) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignal,(Npgsql.NpgsqlParameter[])parameters0.Bind(),token)
            .ConfigureAwait(false);
        var parameters1 = new InsertFuturesTradeSignalIndex($"latest:{timePeriod}", "latest", sequenceId, FuturesTradeSignalV2ReadModel.ContractId, FuturesTradeSignalV2ReadModel.ValueDate, timePeriod) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignalIndex,(Npgsql.NpgsqlParameter[])parameters1.Bind(),token)
            .ConfigureAwait(false);
        var parameters2 = new InsertFuturesTradeSignalIndex($"date:{timePeriod}:{FuturesTradeSignalV2ReadModel.ValueDate.DayNumber}", FuturesTradeSignalV2ReadModel.ContractId, sequenceId, FuturesTradeSignalV2ReadModel.ContractId, FuturesTradeSignalV2ReadModel.ValueDate, timePeriod) { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignalIndex,(Npgsql.NpgsqlParameter[])parameters2.Bind(),token)
            .ConfigureAwait(false);
    }

    static async Task WriteItiAsync(IEnlistedPostgresTransaction transaction, FuturesItiSignalV2ReadModel signal, long eventId, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
        var sequenceId = signal.SequenceId > 0 ? signal.SequenceId : eventId;
        var canonical = MarketDataDbContextExtensions.CreateFuturesItiSignalParameters(signal,sequenceId) with { PostgreSql = true };
        var latest = MarketDataDbContextExtensions.CreateFuturesItiTimeFrameStateParameters(signal,sequenceId) with { PostgreSql = true };
        await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesItiSignal,(Npgsql.NpgsqlParameter[])canonical.Bind(),token)
            .ConfigureAwait(false);
        await transaction.ExecuteAsync(MarketDataDbSql.UpsertFuturesItiTimeFrameState,(Npgsql.NpgsqlParameter[])latest.Bind(),token)
            .ConfigureAwait(false);
    }
}
