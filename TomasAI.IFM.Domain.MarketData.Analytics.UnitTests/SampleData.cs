using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

public static class SampleData
{
    public const string ContractId = "ESU25";
    public static readonly DateOnly ValueDate = new(2025, 6, 20);
    public static readonly TradeTimePeriodType TimePeriod = TradeTimePeriodType.Daily;
    public static readonly int PeriodLength = 14;
    public static readonly DateTime Timestamp = new(2025, 6, 20, 10, 0, 0);
    public const string Symbol = "ES";
    public const double FuturesPrice = 5500.0;
    public const double Lambda = 0.01;
    public const double FuturesPercentChange = 0.5;
    public const double FuturesMean = 5480.0;
    public const double FuturesStdDev = 20.0;
    public const double FuturesMDI = 0.6;
    public const double FuturesRSI = 55.0;
    public const double FuturesRSISlope = 0.1;
    public const decimal FuturesFiftyDMA = 5450.0m;
    public const decimal FuturesTwoHundredDMA = 5300.0m;
    public const double PredictedDelta = 10.0;
    public const double VixFuturesPrice = 15.7;

    public static FuturesItiSignalEntityId EntityId
        => new(ContractId, ValueDate, TimePeriod);

    /// <summary>
    /// Builds a <see cref="FuturesItiSignalEntityId"/> for the given time period, allowing ITI tests to exercise
    /// Daily, Weekly, and Monthly variants without mutating the shared <see cref="TimePeriod"/> default.
    /// </summary>
    public static FuturesItiSignalEntityId EntityIdFor(TradeTimePeriodType timePeriod)
        => new(ContractId, ValueDate, timePeriod);

    public static FuturesItiTrendCoastLineCountersReadModel CoastLineCounters
        => new(upTrendCount: 3, downTrendCount: 3);

    public static GenerateFuturesItiSignalCommand GenerateCommand
        => new(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TimePeriod,
            timestamp: Timestamp,
            futuresPrice: FuturesPrice,
            vixFuturesPrice: VixFuturesPrice);

    /// <summary>
    /// Builds a <see cref="GenerateFuturesItiSignalCommand"/> for the given time period.
    /// </summary>
    public static GenerateFuturesItiSignalCommand GenerateCommandFor(TradeTimePeriodType timePeriod)
        => new(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: timePeriod,
            timestamp: Timestamp,
            futuresPrice: FuturesPrice,
            vixFuturesPrice: VixFuturesPrice);

    public static SetFuturesItiSignalHoldTradeCommand SetHoldTradeCommand
        => new(ContractId, ValueDate, TimePeriod, Timestamp);

    /// <summary>
    /// Builds a <see cref="SetFuturesItiSignalHoldTradeCommand"/> for the given time period.
    /// </summary>
    public static SetFuturesItiSignalHoldTradeCommand SetHoldTradeCommandFor(TradeTimePeriodType timePeriod)
        => new(ContractId, ValueDate, timePeriod, Timestamp);

    public static ClearFuturesItiSignalHoldTradeCommand ClearHoldTradeCommand
        => new(ContractId, ValueDate, TimePeriod, Timestamp);

    /// <summary>
    /// Builds a <see cref="ClearFuturesItiSignalHoldTradeCommand"/> for the given time period.
    /// </summary>
    public static ClearFuturesItiSignalHoldTradeCommand ClearHoldTradeCommandFor(TradeTimePeriodType timePeriod)
        => new(ContractId, ValueDate, timePeriod, Timestamp);

    public static FuturesItiSignalGeneratedEvent StartOfDayEvent
        => new()
        {
            EntityId = EntityId,
            FuturesItiSignal = new FuturesItiSignalV2ReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                sequenceId: 1,
                intrinsicTime: Timestamp,
                intrinsicTimeGroupId: 0,
                intrinsicTimeLength: 0,
                intrinsicPrice: FuturesPrice,
                intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
                intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
                trendPrice: FuturesPrice,
                trendExtreme: FuturesPrice,
                trendReversal: FuturesPrice,
                trendDelta: PredictedDelta + ((FuturesPrice * Lambda) / 2),
                targetDelta: FuturesPrice * Lambda,
                lambda: Lambda,
                tradingDays: 1,
                threshold: 0,
                upTrendTrigger: FuturesPrice,
                downTrendTrigger: FuturesPrice - (FuturesPrice * Lambda),
                tradeState: IntrinsicTimeTradeState.Ready),
            CreatedOn = Timestamp,
            CreatedBy = "test"
        };

    public static FuturesItiSignalGeneratedEvent HoldTradeEvent
        => new()
        {
            EntityId = EntityId,
            FuturesItiSignal = StartOfDayEvent.FuturesItiSignal! with
            {
                TradeState = IntrinsicTimeTradeState.Hold,
                IntrinsicTimeMode = IntrinsicTimeModeType.HoldTradeChanged
            },
            CreatedOn = Timestamp,
            CreatedBy = "test"
        };

    public static FuturesEodDataId EodDataEntityId
        => new(ContractId, ValueDate);

    public static FuturesEodDataInsertedCompleteEvent CreateEodDataInsertedCompleteEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesItiSignalEventActor.Actor, FuturesEodDataInsertedCompleteEvent.Verb, EodDataEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = EodDataEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesEodData = new FuturesEodDataV2ReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                symbol: Symbol,
                openPrice: 5490.0m,
                highPrice: 5510.0m,
                lowPrice: 5480.0m,
                closePrice: (decimal)FuturesPrice,
                volume: 100000),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    public static FuturesItiSignalGeneratedCompleteEvent CreateItiSignalGeneratedCompleteEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesItiSignalEventActor.Actor, FuturesItiSignalGeneratedCompleteEvent.Verb, EntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = EntityId,
            EventId = 2,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesItiSignal = StartOfDayEvent.FuturesItiSignal,
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    // ── RSI Signal ──────────────────────────────────────────────────────

    public const string DailyContractId = "ES20250620";

    public static FuturesRsiSignalEntityId RsiEntityId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength);

    public static FuturesEodDataV2ReadModel EodData
        => new(
            contractId: ContractId,
            valueDate: ValueDate,
            symbol: Symbol,
            openPrice: 5490.0m,
            highPrice: 5510.0m,
            lowPrice: 5480.0m,
            closePrice: (decimal)FuturesPrice,
            volume: 100000);

    public static FuturesEodDataV2ReadModel DailyEodData
        => new(
            contractId: DailyContractId,
            valueDate: ValueDate,
            symbol: Symbol,
            openPrice: 5490.0m,
            highPrice: 5510.0m,
            lowPrice: 5480.0m,
            closePrice: (decimal)FuturesPrice,
            volume: 100000);

    public static StartFuturesRsiSignalCommand RsiStartCommand
        => new(RsiEntityId);

    public static StopFuturesRsiSignalCommand RsiStopCommand
        => new(RsiEntityId);

    public static GenerateFuturesRsiSignalCommand RsiGenerateCommand
        => new(new FuturesRsiSignalId(ContractId, ValueDate, TimePeriod, PeriodLength, TimeOnly.MinValue), (decimal)FuturesPrice);

    public static FuturesRsiSignalStartedEvent RsiStartedEvent
        => new()
        {
            EntityId = RsiEntityId,
            StartedOn = Timestamp,
            StartedBy = "test"
        };

    public static FuturesRsiSignalStartedEvent CreateRsiStartedEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalStartedEvent.Actor, FuturesRsiSignalStartedEvent.Verb, RsiEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = RsiEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            StartedOn = Timestamp,
            StartedBy = "UnitTest"
        };

    public static FuturesRsiSignalStoppedEvent CreateRsiStoppedEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalStoppedEvent.Actor, FuturesRsiSignalStoppedEvent.Verb, RsiEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = RsiEntityId,
            EventId = 2,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            StoppedOn = Timestamp,
            StoppedBy = "UnitTest"
        };

    public static FuturesRsiSignalsGeneratedEvent CreateRsiSignalsGeneratedEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalsGeneratedEvent.Actor, FuturesRsiSignalsGeneratedEvent.Verb, RsiEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = RsiEntityId,
            EventId = 3,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesRsiSignalsId = new FuturesRsiSignalsId(ContractId, ValueDate, new TimeOnly(10, 0, 0)),
            FuturesRsiSignals = [],
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    // ── ATR Signal ──────────────────────────────────────────────────────

    public static FuturesAtrSignalEntityId AtrEntityId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength);

    public static FuturesAtrSignalId AtrSignalId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength, new TimeOnly(10, 0, 0));

    public static FuturesRsiSignalReadModel[] AtrRsiSignals
        => Enumerable.Range(0, 15).Select(i =>
            new FuturesRsiSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                periodLength: PeriodLength,
                timestamp: new TimeOnly(10, 0, i),
                price: 5500.0m + i,
                priceChange: i == 0 ? 0m : 1m,
                priceGain: i == 0 ? 0m : 1m,
                priceLoss: 0m,
                averagePriceGain: 1m,
                averagePriceLoss: 0.5m,
                rs: 2.0,
                rsi: 66.67,
                rsiAverage: 65.0,
                rsiSlope: 0.1)).ToArray();

    public static FuturesItiSignalV2ReadModel[] AtrItiSignals
        => Enumerable.Range(1, 15).Select(i =>
            new FuturesItiSignalV2ReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                sequenceId: i,
                intrinsicTime: Timestamp.AddSeconds(i),
                intrinsicTimeGroupId: 0,
                intrinsicTimeLength: 15.0,
                intrinsicPrice: 5500.0 + i,
                intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
                intrinsicTimeMode: IntrinsicTimeModeType.Trending,
                trendPrice: 5500.0,
                trendExtreme: 5500.0 + i,
                trendReversal: 5490.0,
                trendDelta: (double)i,
                targetDelta: 55.0,
                lambda: Lambda,
                tradingDays: 1,
                threshold: 0,
                upTrendTrigger: 5555.0,
                downTrendTrigger: 5445.0,
                tradeState: IntrinsicTimeTradeState.Ready)).ToArray();

    public static GenerateFuturesAtrSignalCommand AtrGenerateCommand
        => new(AtrSignalId, (decimal)FuturesPrice);

    public static FuturesAtrSignalGeneratedEvent CreateAtrSignalGeneratedEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAtrSignalGeneratedEvent.Actor, FuturesAtrSignalGeneratedEvent.Verb, AtrEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = AtrEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesAtrSignal = new FuturesAtrSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                periodLength: PeriodLength,
                timestamp: new TimeOnly(10, 0, 0),
                futuresPrice: (decimal)FuturesPrice,
                atrValue: 1.0,
                trueRange: 1.5,
                atr: FuturesTrendDirectionType.UpTrending,
                atrStrength: FuturesTrendDirectionStrengthType.Medium),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    public static FuturesAtrSignalGeneratedCompleteEvent CreateAtrSignalGeneratedCompleteEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAtrSignalGeneratedCompleteEvent.Actor, FuturesAtrSignalGeneratedCompleteEvent.Verb, AtrEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = AtrEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesAtrSignal = new FuturesAtrSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                periodLength: PeriodLength,
                timestamp: new TimeOnly(10, 0, 0),
                futuresPrice: (decimal)FuturesPrice,
                atrValue: 1.0,
                trueRange: 1.5,
                atr: FuturesTrendDirectionType.UpTrending,
                atrStrength: FuturesTrendDirectionStrengthType.Medium),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    /// <summary>
    /// ITI signals with a large last price jump — produces TrueRange > AtrValue (UpTrending).
    /// 14 constant prices (5500) then one jump to 5510.
    /// TrueRanges: [0,0,...,0,10] → TrueRange=10, AtrValue≈0.714 → UpTrending.
    /// </summary>
    public static FuturesItiSignalV2ReadModel[] AtrUpTrendItiSignals
        => Enumerable.Range(1, 15).Select(i =>
            new FuturesItiSignalV2ReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                sequenceId: i,
                intrinsicTime: Timestamp.AddSeconds(i),
                intrinsicTimeGroupId: 0,
                intrinsicTimeLength: 15.0,
                intrinsicPrice: i < 15 ? 5500.0 : 5510.0,
                intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
                intrinsicTimeMode: IntrinsicTimeModeType.Trending,
                trendPrice: 5500.0,
                trendExtreme: 5500.0 + i,
                trendReversal: 5490.0,
                trendDelta: (double)i,
                targetDelta: 55.0,
                lambda: Lambda,
                tradingDays: 1,
                threshold: 0,
                upTrendTrigger: 5555.0,
                downTrendTrigger: 5445.0,
                tradeState: IntrinsicTimeTradeState.Ready)).ToArray();

    public static GenerateFuturesAtrSignalCommand AtrUpTrendGenerateCommand
        => new(AtrSignalId, (decimal)FuturesPrice);

    /// <summary>
    /// ITI signals with a small last price change — produces TrueRange &lt; AtrValue (DownTrending).
    /// Prices increasing by 10 each (5500,5510,...,5630) then tiny jump to 5630.1.
    /// TrueRanges: [10,10,...,10,0.1] → TrueRange=0.1, AtrValue≈9.29 → DownTrending.
    /// </summary>
    public static FuturesItiSignalV2ReadModel[] AtrDownTrendItiSignals
        => Enumerable.Range(1, 15).Select(i =>
            new FuturesItiSignalV2ReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                sequenceId: i,
                intrinsicTime: Timestamp.AddSeconds(i),
                intrinsicTimeGroupId: 0,
                intrinsicTimeLength: 15.0,
                intrinsicPrice: i < 15 ? 5500.0 + (i * 10) : 5630.1,
                intrinsicTimeTrend: IntrinsicTimeTrendType.DownTrend,
                intrinsicTimeMode: IntrinsicTimeModeType.Trending,
                trendPrice: 5500.0,
                trendExtreme: 5500.0 + i,
                trendReversal: 5490.0,
                trendDelta: (double)i,
                targetDelta: 55.0,
                lambda: Lambda,
                tradingDays: 1,
                threshold: 0,
                upTrendTrigger: 5555.0,
                downTrendTrigger: 5445.0,
                tradeState: IntrinsicTimeTradeState.Ready)).ToArray();

    public static GenerateFuturesAtrSignalCommand AtrDownTrendGenerateCommand
        => new(AtrSignalId, (decimal)FuturesPrice);

    public static FuturesAtrSignalGeneratedEvent CreateAtrSignalDownTrendEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAtrSignalGeneratedEvent.Actor, FuturesAtrSignalGeneratedEvent.Verb, AtrEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = AtrEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesAtrSignal = new FuturesAtrSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                periodLength: PeriodLength,
                timestamp: new TimeOnly(10, 0, 0),
                futuresPrice: (decimal)FuturesPrice,
                atrValue: 9.0,
                trueRange: 0.1,
                atr: FuturesTrendDirectionType.DownTrending,
                atrStrength: FuturesTrendDirectionStrengthType.Low),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    // ── ADX Signal ───────────────────────────────────────────────────────
    public static FuturesAdxSignalReadModel FuturesAdxSignal
        => new(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TimePeriod,
            periodLength: PeriodLength,
            timestamp: new TimeOnly(10, 0, 0),
            futuresPrice: (decimal)FuturesPrice,
            plusDI: 25.0,
            minusDI: 15.0,
            adxValue: 30.0,
            adx: FuturesTrendDirectionType.UpTrending,
            adxStrength: FuturesTrendDirectionStrengthType.Medium);

    public static FuturesAdxSignalEntityId AdxEntityId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength);

    public static FuturesAdxSignalId AdxSignalId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength, new TimeOnly(10, 0, 0));

    public static GenerateFuturesAdxSignalCommand AdxGenerateCommand
        => new(AdxSignalId, (decimal)FuturesPrice);

    public static FuturesAdxSignalGeneratedEvent CreateAdxSignalGeneratedEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAdxSignalGeneratedEvent.Actor, FuturesAdxSignalGeneratedEvent.Verb, AdxEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = AdxEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesAdxSignal = new FuturesAdxSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                periodLength: PeriodLength,
                timestamp: new TimeOnly(10, 0, 0),
                futuresPrice: (decimal)FuturesPrice,
                plusDI: 25.0,
                minusDI: 15.0,
                adxValue: 30.0,
                adx: FuturesTrendDirectionType.UpTrending,
                adxStrength: FuturesTrendDirectionStrengthType.Medium),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    public static FuturesAdxSignalGeneratedCompleteEvent CreateAdxSignalGeneratedCompleteEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAdxSignalGeneratedCompleteEvent.Actor, FuturesAdxSignalGeneratedCompleteEvent.Verb, AdxEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = AdxEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesAdxSignal = new FuturesAdxSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                periodLength: PeriodLength,
                timestamp: new TimeOnly(10, 0, 0),
                futuresPrice: (decimal)FuturesPrice,    
                plusDI: 25.0,
                minusDI: 15.0,
                adxValue: 30.0,
                adx: FuturesTrendDirectionType.UpTrending,
                adxStrength: FuturesTrendDirectionStrengthType.Medium),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    // Down-trending ITI signals (descending prices → MinusDI > PlusDI → DownTrending)
    public static FuturesItiSignalV2ReadModel[] AdxDownTrendItiSignals
        => Enumerable.Range(1, 15).Select(i =>
            new FuturesItiSignalV2ReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                sequenceId: i,
                intrinsicTime: Timestamp.AddSeconds(i),
                intrinsicTimeGroupId: 0,
                intrinsicTimeLength: 15.0,
                intrinsicPrice: 5500.0 - i,
                intrinsicTimeTrend: IntrinsicTimeTrendType.DownTrend,
                intrinsicTimeMode: IntrinsicTimeModeType.Trending,
                trendPrice: 5500.0,
                trendExtreme: 5500.0 - i,
                trendReversal: 5510.0,
                trendDelta: (double)-i,
                targetDelta: 55.0,
                lambda: Lambda,
                tradingDays: 1,
                threshold: 0,
                upTrendTrigger: 5555.0,
                downTrendTrigger: 5445.0,
                tradeState: IntrinsicTimeTradeState.Ready)).ToArray();

    // Flat ITI signals (constant price → PlusDI == MinusDI == 0 → RangeBound)
    public static FuturesItiSignalV2ReadModel[] AdxFlatItiSignals
        => Enumerable.Range(1, 15).Select(i =>
            new FuturesItiSignalV2ReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                sequenceId: i,
                intrinsicTime: Timestamp.AddSeconds(i),
                intrinsicTimeGroupId: 0,
                intrinsicTimeLength: 15.0,
                intrinsicPrice: 5500.0,
                intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
                intrinsicTimeMode: IntrinsicTimeModeType.Trending,
                trendPrice: 5500.0,
                trendExtreme: 5500.0,
                trendReversal: 5490.0,
                trendDelta: 0.0,
                targetDelta: 55.0,
                lambda: Lambda,
                tradingDays: 1,
                threshold: 0,
                upTrendTrigger: 5555.0,
                downTrendTrigger: 5445.0,
                tradeState: IntrinsicTimeTradeState.Ready)).ToArray();

    // Minimal ITI signals (single element → prices.Length < 2 in ComputeAdx)
    public static FuturesItiSignalV2ReadModel[] AdxMinimalItiSignals
        =>
        [
            new FuturesItiSignalV2ReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                sequenceId: 1,
                intrinsicTime: Timestamp,
                intrinsicTimeGroupId: 0,
                intrinsicTimeLength: 15.0,
                intrinsicPrice: 5500.0,
                intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
                intrinsicTimeMode: IntrinsicTimeModeType.Trending,
                trendPrice: 5500.0,
                trendExtreme: 5500.0,
                trendReversal: 5490.0,
                trendDelta: 0.0,
                targetDelta: 55.0,
                lambda: Lambda,
                tradingDays: 1,
                threshold: 0,
                upTrendTrigger: 5555.0,
                downTrendTrigger: 5445.0,
                tradeState: IntrinsicTimeTradeState.Ready)
        ];

    public static GenerateFuturesAdxSignalCommand AdxDownTrendGenerateCommand
        => new(AdxSignalId, (decimal)FuturesPrice);

    public static GenerateFuturesAdxSignalCommand AdxFlatGenerateCommand
        => new(AdxSignalId, (decimal)FuturesPrice);

    public static GenerateFuturesAdxSignalCommand AdxMinimalGenerateCommand
        => new(AdxSignalId, (decimal)FuturesPrice);

    public static FuturesAdxSignalGeneratedEvent CreateAdxSignalDownTrendEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAdxSignalGeneratedEvent.Actor, FuturesAdxSignalGeneratedEvent.Verb, AdxEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = AdxEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesAdxSignal = new FuturesAdxSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                periodLength: PeriodLength,
                timestamp: new TimeOnly(10, 0, 0),
                futuresPrice: (decimal)FuturesPrice,
                plusDI: 15.0,
                minusDI: 25.0,
                adxValue: 30.0,
                adx: FuturesTrendDirectionType.DownTrending,
                adxStrength: FuturesTrendDirectionStrengthType.Medium),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    public static FuturesAdxSignalGeneratedEvent CreateAdxSignalTrendReversalEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAdxSignalGeneratedEvent.Actor, FuturesAdxSignalGeneratedEvent.Verb, AdxEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = AdxEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesAdxSignal = new FuturesAdxSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                periodLength: PeriodLength,
                timestamp: new TimeOnly(10, 0, 0),
                futuresPrice: (decimal)FuturesPrice,
                plusDI: 20.0,
                minusDI: 20.0,
                adxValue: 30.0,
                adx: FuturesTrendDirectionType.TrendReversal,
                adxStrength: FuturesTrendDirectionStrengthType.Medium),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    // ── MACD Signal ─────────────────────────────────────────────────────

    public static FuturesMacdSignalEntityId MacdEntityId
        => new(ContractId, ValueDate, TradeTimePeriodType.Daily, 14);

    public static FuturesMacdSignalId MacdSignalId
        => new(ContractId, ValueDate, TradeTimePeriodType.Daily, 14, new TimeOnly(18, 50, 10));

    public static GenerateFuturesMacdSignalCommand MacdGenerateCommand
        => new(MacdSignalId, (decimal)FuturesPrice);

    public static FuturesMacdSignalGeneratedEvent CreateMacdSignalGeneratedEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesMacdSignalGeneratedEvent.Actor, FuturesMacdSignalGeneratedEvent.Verb, MacdEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = MacdEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesMacdSignal = new FuturesMacdSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TradeTimePeriodType.Daily,
                periodLength: 14,
                futuresPrice: (decimal)FuturesPrice,
                timestamp: new TimeOnly(18, 50, 10),
                macdLine: 1.5,
                signalLine: 1.2,
                histogram: 0.3,
                macd: FuturesTrendDirectionType.UpTrending,
                macdStrength: FuturesTrendDirectionStrengthType.Medium),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    public static FuturesMacdSignalGeneratedCompleteEvent CreateMacdSignalGeneratedCompleteEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesMacdSignalGeneratedCompleteEvent.Actor, FuturesMacdSignalGeneratedCompleteEvent.Verb, MacdEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = MacdEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesMacdSignal = new FuturesMacdSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TradeTimePeriodType.Daily,
                periodLength: 14,
                futuresPrice: (decimal)FuturesPrice,    
                timestamp: new TimeOnly(18, 50, 10),
                macdLine: 1.5,
                signalLine: 1.2,
                histogram: 0.3,
                macd: FuturesTrendDirectionType.UpTrending,
                macdStrength: FuturesTrendDirectionStrengthType.Medium),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    // ── TDI Signal ───────────────────────────────────────────────────────

    public static FuturesTdiSignalEntityId TdiEntityId
        => new(ContractId, ValueDate, TradeTimePeriodType.Daily);

    public static FuturesTdiSignalEntityId TdiEntityIdFor(TradeTimePeriodType timePeriod)
        => new(ContractId, ValueDate, timePeriod);

    public static FuturesTdiSignalId TdiSignalId
        => new(ContractId, ValueDate, new TimeOnly(10, 0, 0));

    public static GenerateFuturesTdiSignalCommand TdiGenerateCommand
        => new(TdiSignalId, AtrRsiSignals);

    public static GenerateFuturesTdiSignalCommand TdiGenerateCommandFor(
        TradeTimePeriodType timePeriod,
        FuturesRsiSignalReadModel[]? rsiSignals = null,
        Guid? commandId = null)
    {
        var entityId = TdiEntityIdFor(timePeriod);
        var normalizedSignals = (rsiSignals ?? AtrRsiSignals)
            .Select(signal => signal with { TimePeriod = timePeriod })
            .ToArray();
        return new GenerateFuturesTdiSignalCommand(TdiSignalId, normalizedSignals)
        {
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesTdiSignalCommand.Actor,
                GenerateFuturesTdiSignalCommand.Verb,
                entityId.Format())
        };
    }

    public static FuturesTdiSignalGeneratedEvent CreateTdiSignalGeneratedEventFor(
        TradeTimePeriodType timePeriod,
        FuturesTrendDirectionType direction = FuturesTrendDirectionType.UpTrending,
        Guid? commandId = null)
    {
        var entityId = TdiEntityIdFor(timePeriod);
        return new FuturesTdiSignalGeneratedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesTdiSignalGeneratedEvent.Actor,
                FuturesTdiSignalGeneratedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesTdiSignal = new FuturesTdiSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: timePeriod,
                timestamp: TdiSignalId.Timestamp,
                upTrendCount: 8,
                downTrendCount: 7,
                tdi: direction,
                tdiStrength: FuturesTrendDirectionStrengthType.Medium),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };
    }

    public static FuturesTdiSignalGeneratedEvent CreateTdiSignalGeneratedEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTdiSignalGeneratedEvent.Actor, FuturesTdiSignalGeneratedEvent.Verb, TdiEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = TdiEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesTdiSignal = new FuturesTdiSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TradeTimePeriodType.Daily,
                timestamp: new TimeOnly(10, 0, 0),
                upTrendCount: 8,
                downTrendCount: 7,
                tdi: FuturesTrendDirectionType.UpTrending,
                tdiStrength: FuturesTrendDirectionStrengthType.Medium),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    public static FuturesTdiSignalGeneratedCompleteEvent CreateTdiSignalGeneratedCompleteEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTdiSignalGeneratedCompleteEvent.Actor, FuturesTdiSignalGeneratedCompleteEvent.Verb, TdiEntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = TdiEntityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesTdiSignal = new FuturesTdiSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TradeTimePeriodType.Daily,
                timestamp: new TimeOnly(10, 0, 0),
                upTrendCount: 8,
                downTrendCount: 7,
                tdi: FuturesTrendDirectionType.UpTrending,
                tdiStrength: FuturesTrendDirectionStrengthType.Medium),
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    // ── Trade Signal ────────────────────────────────────────────────────

    public static FuturesTradeSignalEntityId TradeSignalEntityId
        => new(ContractId, ValueDate, TimePeriod);

    public static UpdateFuturesTradeSignalCommand TradeSignalUpdateCommand
        => new(EodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, FuturesTradeSignalCommandActor.ActorName, UpdateFuturesTradeSignalCommand.Verb, TradeSignalEntityId.Format()),
            EntityId = TradeSignalEntityId
        };

    public static UpdateFuturesTradeSignalCommand CreateTradeSignalUpdateCommand(
        FuturesEodDataV2ReadModel? eodData = null,
        FuturesRsiSignalReadModel? rsiSignal = null,
        FuturesTdiSignalReadModel? tdiSignal = null,
        FuturesItiSignalDataReadModel? itiSignalData = null,
        decimal vixFuturesPrice = 0)
    {
        var eod = eodData ?? EodData;
        return new UpdateFuturesTradeSignalCommand(eod, rsiSignal, tdiSignal, itiSignalData, vixFuturesPrice)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, FuturesTradeSignalCommandActor.ActorName, UpdateFuturesTradeSignalCommand.Verb, TradeSignalEntityId.Format()),
            EntityId = TradeSignalEntityId
        };
    }

    public static FuturesTradeSignalUpdatedCompleteEvent CreateTradeSignalUpdatedCompleteEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTradeSignalEventActor.Actor, "TradeSignalUpdatedComplete", TradeSignalEntityId.Format()),
            CommandId = commandId ?? Guid.NewGuid(),
            FuturesTradeSignal = null,
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };

    public static FuturesItiSignalHoldTradeChangedEvent CreateHoldTradeChangedEvent(Guid? commandId = null)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTradeSignalEventActor.Actor, "HoldTradeChanged", EntityId.Format()),
            CommandId = commandId ?? Guid.NewGuid(),
            FuturesItiSignalId = new FuturesItiSignalId(ContractId, ValueDate, TimePeriod, Timestamp),
            HoldTrade = true,
            CreatedOn = Timestamp,
            CreatedBy = "UnitTest"
        };
}
