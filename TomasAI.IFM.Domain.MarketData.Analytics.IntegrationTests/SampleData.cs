using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;
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

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests;

public static class SampleData
{
    public const string ContractId = "ESU25";
    public static readonly DateOnly ValueDate = new(2025, 6, 20);
    public static readonly TradeTimePeriodType TimePeriod = TradeTimePeriodType.Weekly;
    public static readonly int PeriodLength = 14;
    public static readonly TradeTimePeriodType RSITimePeriod = TradeTimePeriodType.FifteenSeconds;
    public static readonly DateTime Timestamp = new(2025, 6, 20, 10, 0, 0);
    public const string Symbol = "ES";
    public const double FuturesPrice = 5500.0;
    public const double VixFuturesPrice = 20.0;
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

    public static FuturesItiSignalEntityId EntityId
        => new(ContractId, ValueDate, TimePeriod);

    public static FuturesItiTrendCoastLineCountersReadModel CoastLineCounters
        => new(upTrendCount: 3, downTrendCount: 3);

    public static GenerateFuturesItiSignalCommand GenerateCommand
        => new(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TimePeriod,
            timestamp: Timestamp,
            futuresPrice: FuturesPrice,
            vixFuturesPrice: 0);

    public static SetFuturesItiSignalHoldTradeCommand SetHoldTradeCommand
        => new(ContractId, ValueDate, TimePeriod, Timestamp);

    public static ClearFuturesItiSignalHoldTradeCommand ClearHoldTradeCommand
        => new(ContractId, ValueDate, TimePeriod, Timestamp);

    public static FuturesItiSignalGeneratedEvent StartOfDayEvent
        => new()
        {
            EntityId = EntityId,
            FuturesItiSignal = new FuturesItiSignalV2ReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                sequenceId: 0,
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
                tradingDays: 0,
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

    public static FuturesRsiSignalEntityId RsiEntityId
        => new(ContractId, ValueDate, RSITimePeriod, PeriodLength);

    public static FuturesEodDataV2ReadModel FuturesEodData
        => new(
            contractId: ContractId,
            valueDate: ValueDate,
            symbol: Symbol,
            openPrice: 5490.0m,
            highPrice: 5510.0m,
            lowPrice: 5480.0m,
            closePrice: (decimal)FuturesPrice,
            volume: 100000);

    public static FuturesAtrSignalEntityId AtrEntityId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength);
    public static FuturesAtrSignalId AtrSignalId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength, TimeOnly.FromDateTime(Timestamp));

    public static FuturesRsiSignalReadModel[] CreateRsiSignalsForAtr(int count = 15)
    {
        var signals = new FuturesRsiSignalReadModel[count];
        var baseTime = TimeOnly.FromDateTime(Timestamp);
        for (int i = 0; i < count; i++)
        {
            signals[i] = new FuturesRsiSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: RSITimePeriod,
                periodLength: PeriodLength,
                timestamp: baseTime.Add(TimeSpan.FromSeconds(i * 15)),
                price: 5500.0m + i,
                priceChange: i == 0 ? 0m : 1m,
                priceGain: i == 0 ? 0m : 1m,
                priceLoss: 0m,
                averagePriceGain: 0.5m,
                averagePriceLoss: 0.3m,
                rs: 1.67,
                rsi: FuturesRSI + i * 0.1,
                rsiAverage: FuturesRSI,
                rsiSlope: FuturesRSISlope);
        }
        return signals;
    }

    public static FuturesItiSignalV2ReadModel[] CreateItiSignalsForAtr(int count = 15)
    {
        var signals = new FuturesItiSignalV2ReadModel[count];
        for (int i = 0; i < count; i++)
        {
            signals[i] = new FuturesItiSignalV2ReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: TimePeriod,
                sequenceId: i,
                intrinsicTime: Timestamp.AddSeconds(i * 15),
                intrinsicTimeGroupId: 0,
                intrinsicTimeLength: 0,
                intrinsicPrice: FuturesPrice + i,
                intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
                intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
                trendPrice: FuturesPrice + i,
                trendExtreme: FuturesPrice + i,
                trendReversal: FuturesPrice,
                trendDelta: PredictedDelta + ((FuturesPrice * Lambda) / 2),
                targetDelta: FuturesPrice * Lambda,
                lambda: Lambda,
                tradingDays: 0,
                threshold: 0,
                upTrendTrigger: FuturesPrice + i,
                downTrendTrigger: FuturesPrice - (FuturesPrice * Lambda),
                tradeState: IntrinsicTimeTradeState.Ready);
        }
        return signals;
    }

    public static FuturesAtrSignalReadModel CreateAtrSignalViewModel(
        FuturesTrendDirectionType atr = FuturesTrendDirectionType.Init,
        FuturesTrendDirectionStrengthType atrStrength = FuturesTrendDirectionStrengthType.Low)
        => new(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: RSITimePeriod,
            periodLength: PeriodLength,
            timestamp: TimeOnly.FromDateTime(Timestamp),
            futuresPrice: (decimal)FuturesPrice,
            atrValue: 10.5,
            trueRange: 12.0,
            atr: atr,
            atrStrength: atrStrength);

    public static FuturesAdxSignalEntityId AdxEntityId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength);

    public static FuturesAdxSignalId AdxSignalId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength,TimeOnly.FromDateTime(Timestamp));

    public static FuturesRsiSignalReadModel[] CreateRsiSignalsForAdx(int count = 15)
    {
        var signals = new FuturesRsiSignalReadModel[count];
        var baseTime = TimeOnly.FromDateTime(Timestamp);
        for (int i = 0; i < count; i++)
        {
            signals[i] = new FuturesRsiSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: RSITimePeriod,
                periodLength: PeriodLength,
                timestamp: baseTime.Add(TimeSpan.FromSeconds(i * 15)),
                price: 5500.0m + i,
                priceChange: i == 0 ? 0m : 1m,
                priceGain: i == 0 ? 0m : 1m,
                priceLoss: 0m,
                averagePriceGain: 0.5m,
                averagePriceLoss: 0.3m,
                rs: 1.67,
                rsi: FuturesRSI + i * 0.1,
                rsiAverage: FuturesRSI,
                rsiSlope: FuturesRSISlope);
        }
        return signals;
    }

    public static FuturesAdxSignalReadModel CreateAdxSignalViewModel(
        FuturesTrendDirectionType adx = FuturesTrendDirectionType.UpTrending,
        FuturesTrendDirectionStrengthType adxStrength = FuturesTrendDirectionStrengthType.Medium)
        => new(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TimePeriod,
            periodLength: PeriodLength,
            futuresPrice: 5500.0m,
            timestamp: TimeOnly.FromDateTime(Timestamp),
            plusDI: 25.0,
            minusDI: 15.0,
            adxValue: 30.0,
            adx: adx,
            adxStrength: adxStrength);

    public static FuturesMacdSignalEntityId MacdEntityId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength);

    public static FuturesMacdSignalId MacdSignalId
        => new(ContractId, ValueDate, TimePeriod, PeriodLength, TimeOnly.FromDateTime(Timestamp));

    public static FuturesRsiSignalReadModel[] CreateRsiSignalsForMacd(int count = 15)
    {
        var signals = new FuturesRsiSignalReadModel[count];
        var baseTime = TimeOnly.FromDateTime(Timestamp);
        for (int i = 0; i < count; i++)
        {
            signals[i] = new FuturesRsiSignalReadModel(
                contractId: ContractId,
                valueDate: ValueDate,
                timePeriod: RSITimePeriod,
                periodLength: PeriodLength,
                timestamp: baseTime.Add(TimeSpan.FromSeconds(i * 15)),
                price: 5500.0m + i,
                priceChange: i == 0 ? 0m : 1m,
                priceGain: i == 0 ? 0m : 1m,
                priceLoss: 0m,
                averagePriceGain: 0.5m,
                averagePriceLoss: 0.3m,
                rs: 1.67,
                rsi: FuturesRSI + i * 0.1,
                rsiAverage: FuturesRSI,
                rsiSlope: FuturesRSISlope);
        }
        return signals;
    }

    public static FuturesMacdSignalReadModel CreateMacdSignalViewModel(
        FuturesTrendDirectionType macd = FuturesTrendDirectionType.UpTrending,
        FuturesTrendDirectionStrengthType macdStrength = FuturesTrendDirectionStrengthType.Medium)
        => new(
            contractId: ContractId,
            valueDate: ValueDate,
            timePeriod: TimePeriod,
            periodLength: PeriodLength,
            timestamp: TimeOnly.FromDateTime(Timestamp),
            futuresPrice: 5500.0m,
            macdLine: 1.5,
            signalLine: 1.2,
            histogram: 0.3,
            macd: macd,
            macdStrength: macdStrength);
}
