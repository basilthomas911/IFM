using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Storage;
using QLNet;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Storage.ScyllaDb;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>
/// market data db context
/// </summary>
public class MarketDataDbContext : ObjectDataRepository<MarketDataDbContext>, IMarketDataDbContext, IMarketDataDbReadContext, IMarketDataDbWriteContext
{
    public const string MarketDataDbConnection = "MarketDataDbConnection";
    static Dictionary<TradingDaysKey, int> _tradingDaysMap = new();
    static Dictionary<(string contractId, DateTime startDate, DateTime endDate), ICollection<FuturesEodDataViewModel>> _futuresEodDataMap = new();
    static NormalCurveTableReadModel _normalCurveTable;
    IDbContextFactory _dbFactory;

    /// <summary>
    /// market data database constructor
    /// </summary>
    /// <param name="connectionSettings"></param>
    /// <param name="dbFactory"></param>
    public MarketDataDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<MarketDataDbContext> logger)
        :base(connectionSettings[MarketDataDbConnection], logger)
    {
        _dbFactory = IsArgumentNull.Set(dbFactory);
    }

    /// <summary>
    /// initialize view model mappings
    /// </summary>
    /// <param name="model"></param>
    public override void OnCreateModel(DbModel<MarketDataDbContext> model)
    {
        VixFuturesEodData = model.Map(e => e.VixFuturesEodData)
          .Parameters(e =>
              e.Set(o => o.ContractId)
               //.Set(o => o.ValueDate)
               .Set(o => o.OpenPrice)
               .Set(o => o.HighPrice)
               .Set(o => o.LowPrice)
               .Set(o => o.ClosePrice)
               .Set(o => o.Volume)
          );

        FuturesBarData = model.Map(e => e.FuturesBarData)
          .Parameters(e =>
              e.Set(o => o.ContractId)
               .Set(o => o.Symbol)
             //  .Set(o => o.ValueDate)
               .Set(o => o.BarDate)
               .Set(o => o.BarRateType, o => o.AsEnum<BarRateType>())
               .Set(o => o.BarValue)
               .Set(o => o.UpTrendTrigger)
               .Set(o => o.DownTrendTrigger)
          );

        NormalCurveData = model.Map(e => e.NormalCurveData)
           .Parameters(e =>
               e.Set("StdDevIndex")
                .Set("StdDevPercent")
           );

        MarketHoliday = model.Map(e => e.MarketHoliday)
            .Parameters(e =>
               e.Set(o => o.CurrencyType, o => o.AsEnum<CurrencyType>())
                //.Set(o => o.HolidayDate)
                .Set(o => o.Description)
            );

        MarketExchange = model.Map(e => e.MarketExchange)
            .Parameters(e =>
               e.Set(o => o.Symbol)
                .Set(o => o.Exchange)
                .Set(o => o.DayOfWeek, o => o.AsEnum<DayOfWeek>())
                .Set(o => o.MarketOpen)
                .Set(o => o.MarketClose)
            );

        FuturesContract = model.Map(e => e.FuturesContract)
            .Parameters(e =>
                e.Set(o => o.ContractId)
                 .Set(o => o.Description)
                 .Set(o => o.Symbol)
                 .Set(o => o.LocalSymbol)
                 .Set(o => o.SecurityType)
                 .Set(o => o.Currency)
                 .Set(o => o.Exchange)
                 .Set(o => o.Multiplier)
                 .Set(o => o.LastTradeDate)
                 .Set(o => o.CurrentlyTraded)
            );

        FuturesOptionContract = model.Map(e => e.FuturesOptionContract)
            .Parameters(e =>
                e.Set(o => o.ContractId)
                 .Set(o => o.Description)
                 .Set(o => o.Symbol)
                 .Set(o => o.LocalSymbol)
                 .Set(o => o.SecurityType)
                 .Set(o => o.Currency)
                 .Set(o => o.Exchange)
                 .Set(o => o.Multiplier)
                 //.Set(o => o.ContractMonth)
                 .Set(o => o.StrikePrice)
                 .Set(o => o.OptionType)
            );

        FuturesEodData = model.Map(e => e.FuturesEodData)
            .Parameters(e =>
                e.Set(o => o.ContractId)
                 .Set(o => o.ValueDate)
                 .Set(o => o.OpenPrice)
                 .Set(o => o.HighPrice)
                 .Set(o => o.LowPrice)
                 .Set(o => o.ClosePrice)
                 .Set(o => o.Volume)
                 .Set(o => o.DailyPercentChange)
                 .Set(o => o.DailyStdDev)
                 .Set(o => o.DailyStdDevAmount)
                 .Set(o => o.UpperBand)
                 .Set(o => o.Mean)
                 .Set(o => o.LowerBand)
                 .Set(o => o.MarketDirection, o => o.AsEnum<MarketDirectionType>())
                 .Set(o => o.MarketVolatility, o => o.AsEnum<MarketVolatilityType>())
                 .Set(o => o.PriceDirection, o => o.AsEnum<PriceDirectionType>())
                 .Set(o => o.PriceVolatility, o => o.AsEnum<PriceVolatilityType>())
                 .Set(o => o.MarketDirectionIndicator)
                 .Set(o => o.WindowSize)
          );

        YieldCurveRate = model.Map(e => e.YieldCurveRate)
            .Parameters(e =>
                //e.Set(o => o.ValueDate)
                 e.Set(o => o.OneMonth)
                 .Set(o => o.TwoMonth)
                 .Set(o => o.ThreeMonth)
                 .Set(o => o.SixMonth)
                 .Set(o => o.OneYear)
                 .Set(o => o.TwoYear)
                 .Set(o => o.ThreeYear)
                 .Set(o => o.FiveYear)
                 .Set(o => o.SevenYear)
                 .Set(o => o.TenYear)
                 .Set(o => o.TwentyYear)
                 .Set(o => o.ThirtyYear)
            );

        ReturnRate = model.Map(e => e.ReturnRate)
            .Parameters(e =>
                e.Set(o => o.Symbol)
                 //.Set(o => o.ValueDate)
                 .Set(o => o.RateOfReturn)
            );

        FuturesOptionTickData = model.Map(e => e.FuturesOptionTickData)
            .Parameters(e =>
                e.Set(o => o.ContractId)
                 .Set(o => o.TickDate)
                 .Set(o => o.TickTime)
                 .Set(o => o.OptionPrice)
                 .Set(o => o.BidPrice)
                 .Set(o => o.AskPrice)
                 .Set(o => o.BidSize)
                 .Set(o => o.AskSize)
                 .Set(o => o.ImpliedVolatility)
                 .Set(o => o.UnderlyingPrice)
                 .Set(o => o.Delta)
                 .Set(o => o.Gamma)
                 .Set(o => o.Vega)
                 .Set(o => o.Theta)
                 .Set(o => o.Rho)
            );

        FuturesTickData = model.Map(e => e.FuturesTickData)
            .Parameters(e =>
                e.Set(o => o.ValueDate)
                 .Set(o => o.ContractId)
                 .Set(o => o.TickDate)
                 .Set(o => o.TickTime)
                 .Set(o => o.Price)
                 .Set(o => o.Size)
            );

        MarketVolatilityStrikePriceOffset = model.Map(e => e.MarketVolatilityStrikePriceOffset)
            .Parameters(e =>
                e.Set(o => o.Symbol)
                 .Set(o => o.MarketTrend, o => o.AsEnum<MarketDirectionType>())
                 .Set(o => o.MarketVolatility, o => o.AsEnum<MarketVolatilityType>())
                 .Set(o => o.StrikePriceOffset)
             );

        TradeLiveFeed = Map(e => e.TradeLiveFeed)
            .Parameters(e =>
                e.Set(o => o.OrderId)
                 .Set(o => o.TradeId)
                 .Set(o => o.LiveFeed)
            );

        FuturesClosingPrice = Map(e => e.FuturesClosingPrice)
            .Parameters(e =>
                e.Set(o => o.ContractId)
                 //.Set(o => o.ValueDate)
                 .Set(o => o.ClosingPrice)
                 .Set(o => o.CreatedOn)
                 .Set(o => o.CreatedBy)
            );

        FuturesTradeSignal = Map(e => e.FuturesTradeSignal)
            .Parameters(e =>
                e.Set(o => o.ContractId)
                 //.Set(o => o.ValueDate)
                 .Set(o => o.Mean)
                 .Set(o => o.StdDev)
                 .Set(o => o.FuturesPrice)
                 .Set(o => o.PriceChangePercent)
                 .Set(o => o.FundRiskPercent)
                 .Set(o => o.RSI)
                 .Set(o => o.RSISlope)
                 .Set(o => o.TrendType, o => o.AsEnum<FuturesTrendType>())
                 .Set(o => o.TrendStrength, o => o.AsEnum<FuturesTrendStrengthType>())
                 .Set(o => o.TradeSignal, o => o.AsEnum<TradeSignalType>())
                 .Set(o => o.TDI, o => o.AsEnum<FuturesTrendDirectionType>())
                 .Set(o => o.TDIStrength, o => o.AsEnum<FuturesTrendDirectionStrengthType>())
                 .Set(o => o.MDI)
                 .Set(o => o.MDITrend, o=>o.AsEnum<FuturesMDITrendType>())
                 .Set(o => o.MDIUpTrendLimit)
                 .Set(o => o.MDIDownTrendLimit)
                 .Set(o => o.UpTrendingTrigger)
                 .Set(o => o.DownTrendingTrigger)
                 .Set(o => o.EntryTrigger)
                 .Set(o => o.ExitTrigger)
                 .Set(o => o.TrendDelta)
                 .Set(o => o.TrendExtreme)
                 .Set(o => o.TrendReversal)
                 .Set(o => o.FiftyDMA)
                 .Set(o => o.TwoHundredDMA)
                 .Set(o => o.TradeExecuteState, o => o.AsEnum<TradeExecuteState>())
                 .Set(o => o.CreatedOn)
                 .Set(o => o.CreatedBy)
            );

        FuturesTradeSignalId = Map(e => e.FuturesTradeSignalId)
            .Parameters(e =>
                e.Set(o => o.ContractId)
                 //.Set(o => o.ValueDate)
            );

        FuturesRsiSignal = Map(e => e.FuturesRsiSignal)
           .Parameters(e =>
               e.Set(o => o.ContractId)
                //.Set(o => o.ValueDate)
               // .Set(o => o.Timestamp)
                .Set(o => o.SignalType, o => o.AsEnum<FuturesRsiSignalType>())
                .Set(o => o.Price)
                .Set(o => o.PriceChange)
                .Set(o => o.PriceGain)
                .Set(o => o.PriceLoss)
                .Set(o => o.AveragePriceGain)
                .Set(o => o.AveragePriceLoss)
                .Set(o => o.RS)
                .Set(o => o.RSI)
                .Set(o => o.RSIAverage)
                .Set(o => o.RSISlope)
                .Set(o => o.WindowSize)
           );



        FuturesTdiSignal = Map(e => e.FuturesTdiSignal)
           .Parameters(e =>
               e.Set(o => o.ContractId)
                //.Set(o => o.ValueDate)
                //.Set(o => o.Timestamp)
                .Set(o => o.UpTrendCount)
                .Set(o => o.DownTrendCount)
                .Set(o => o.TDI, o => o.AsEnum<FuturesTrendDirectionType>())
                .Set(o => o.TDIStrength, o => o.AsEnum<FuturesTrendDirectionStrengthType>())
           );

        FuturesTrendDirection = Map(e => e.FuturesTrendDirection)
           .Parameters(e =>
               e.Set(o => o.ContractId)
               // .Set(o => o.ValueDate)
              //  .Set(o => o.Timestamp)
                .Set(o => o.LookbackInterval)
                .Set(o => o.UpTrendCount)
                .Set(o => o.DownTrendCount)
                .Set(o => o.TrendDirection, o => o.AsEnum<FuturesTrendType>())
           );

        FuturesTradeSignalLLM = Map(e => e.FuturesTradeSignalLLM)
            .Parameters(e =>
                 e.Set(o => o.ContractId)
                   // .Set(o => o.ValueDate)
                    .Set(o => o.Timestamp)
                    .Set(o =>o.OpenPrice)
                    .Set(o => o.HighPrice)
                    .Set(o=> o.LowPrice)
                    .Set(o => o.ClosePrice) 
                    .Set(o => o.Volume)
                    .Set(o => o.DailyPercentChange)
                    .Set(o => o.DailyStdDev)
                    .Set(o => o.UpperBand)
                    .Set(o => o.Mean)
                    .Set(o => o.LowerBand)
                    .Set(o => o.PriceVolatility)
                    .Set(o => o.CreatedOn)
                    .Set(o => o.CreatedBy)
                );

        FuturesTradeSignalMetricsLLM = Map(e => e.FuturesTradeSignalMetricsLLM)
           .Parameters(e =>
                e.Set(o => o.ContractId)
                   //.Set(o => o.ValueDate)
                   .Set(o => o.Timestamp)
                   .Set(o => o.MarketDirection, o => o.AsEnum<MarketDirectionType>())
                   .Set(o => o.MarketVolatility, o => o.AsEnum<MarketVolatilityType>())
                   .Set(o => o.PriceDirection, o => o.AsEnum<PriceDirectionType>())
                   .Set(o => o.PriceVolatility, o => o.AsEnum<PriceVolatilityType>())
                   .Set(o => o.MarketDirectionIndicator)
                   .Set(o => o.CreatedOn)
                   .Set(o => o.CreatedBy)
           );


        FuturesItiSignal = Map(e => e.FuturesItiSignal)
           .Parameters(e =>
               e.Set(o => o.ContractId)
                //.Set(o => o.ValueDate)
                .Set(o => o.IntrinsicTime)
                .Set(o => o.IntrinsicTimeGroupId)
                .Set(o => o.IntrinsicTimeLength)
                .Set(o => o.IntrinsicPrice)
                .Set(o => o.IntrinsicTimeTrend, o => o.AsEnum<IntrinsicTimeTrendType>())
                .Set(o => o.IntrinsicTimeMode, o => o.AsEnum<IntrinsicTimeModeType>())
                .Set(o => o.TrendPrice)
                .Set(o => o.TrendExtreme)
                .Set(o => o.TrendReversal)
                .Set(o => o.Lambda)
                .Set(o => o.TargetDelta)
                .Set(o => o.PredictedDelta)
                .Set(o => o.TrendDelta)
                .Set(o => o.UpTrendTrigger)
                .Set(o => o.DownTrendTrigger)
                .Set(o => o.FuturesPercentChange)
                .Set(o => o.FuturesMean)
                .Set(o => o.FuturesStdDev)
                .Set(o => o.FuturesMDI)
                .Set(o => o.FuturesMDITrend, o =>o.AsEnum<FuturesMDITrendType>())
                .Set(o => o.FuturesMDIUpTrendLimit)
                .Set(o => o.FuturesMDIDownTrendLimit)
                .Set(o => o.FuturesRSI)
                .Set(o => o.FuturesRSISlope)
                .Set(o => o.FuturesFiftyDMA)
                .Set(o => o.FuturesTwoHundredDMA)
                .Set(o => o.TradeState, o => o.AsEnum<IntrinsicTimeTradeState>())
                .Set(o => o.UpTrendCoastLineCounter)
                .Set(o => o.DownTrendCoastLineCounter)
           );

        FuturesItiTrendDeltaData = Map(e => e.FuturesItiTrendDeltaData)
            .Parameters(e =>
                e.Set(o => o.Symbol)
                 //.Set(o => o.ValueDate)
                 .Set(o => o.Timestamp)
                 .Set(o => o.TrendDelta)
                 .Set(o => o.TrendDirection)
                 .Set(o => o.TrendDirectionMode)
                 .Set(o => o.FuturesPrice)
                 .Set(o => o.TrendExtreme)
                 .Set(o => o.FuturesRSI)
            );

        FuturesItiTrendDeltaModel = Map(e => e.FuturesItiTrendDeltaModel)
          .Parameters(e =>
              e.Set(o => o.Symbol)
               //.Set(o => o.ValueDate)
               //.Set(o => o.StartDate)
               //.Set(o => o.EndDate)
               .Set(o => o.Count)
               .Set(o => o.Maximum)
               .Set(o => o.Mean)
               .Set(o => o.Median)
               .Set(o => o.Minimum)
               .Set(o => o.Skewness)
               .Set(o => o.StdDev)
               .Set(o => o.Variance)
               .Set(o => o.MeanAbsoluteError)
               .Set(o => o.MeanSquaredError)
               .Set(o => o.RootMeanSquaredError)
               .Set(o => o.LossFunction)
               .Set(o => o.RSquared)
               .Set(o => o.ModelData, o => o.AsBinary())
          );

        FuturesItiTrendClassData = Map(e => e.FuturesItiTrendClassData)
            .Parameters(e =>
                e.Set(o => o.Symbol)
                 //.Set(o => o.ValueDate)
                 .Set(o => o.Timestamp)
                 .Set(o => o.TrendClass)
                 .Set(o => o.TrendDirection)
                 .Set(o => o.TrendDirectionMode)
                 .Set(o => o.TrendDelta)
                 .Set(o => o.FuturesRSI)
            );

        FuturesEodMovingAverage = Map(e => e.FuturesEodMovingAverage)
           .Parameters(e =>
               e.Set(o => o.Symbol)
                .Set(o => o.MovingAverage)
        );

        FuturesEodClosingPrice = Map(e => e.FuturesEodClosingPrice)
          .Parameters(e =>
              e.Set(o => o.Symbol)
               .Set(o => o.ValueDate)
               .Set(o => o.ClosingPrice)
       );

        FuturesItiSignalAveragePredictedTrendDelta = Map(e => e.FuturesItiSignalAveragePredictedTrendDelta)
            .Parameters(e =>
                e.Set(o => o.ContractId)
                    //.Set(o => o.ValueDate)
                    .Set(o => o.PredictedUpTrendDelta)
                    .Set(o => o.PredictedDownTrendDelta)
                    .Set(o => o.UpTrendFuturesRSI)
                    .Set(o => o.DownTrendFuturesRSI)
       );

        FuturesItiSignalAverageRSI = Map(e => e.FuturesItiSignalAverageRSI)
            .Parameters(e =>
                e.Set(o => o.ContractId)
                    .Set(o => o.ValueDate)
                    .Set(o => o.UpTrendRSI)
                    .Set(o => o.DownTrendRSI)
        );

        FuturesItiSignalAveragePredictedTrendDeltaRange = Map(e => e.FuturesItiSignalAveragePredictedTrendDeltaRange)
          .Parameters(e =>
              e.Set(o => o.Symbol)
                  .Set(o => o.StartDate)
                  .Set(o => o.EndDate)
                  .Set(o => o.PredictedUpTrendDelta)
                  .Set(o => o.PredictedDownTrendDelta)
                );

        FuturesItiSignalAverageTrendDeltaRange = Map(e => e.FuturesItiSignalAverageTrendDeltaRange)
          .Parameters(e =>
              e.Set(o => o.Symbol)
                  .Set(o => o.StartDate)
                  .Set(o => o.EndDate)
                  .Set(o => o.UpTrendDelta)
                  .Set(o => o.DownTrendDelta)
                );

        FuturesItiSignalTrendDeltaByDirectionChanged = Map(e => e.FuturesItiSignalTrendDeltaByDirectionChanged)
          .Parameters(e =>
              e.Set(o => o.Symbol)
                  .Set(o => o.StartDate)
                  .Set(o => o.EndDate)
                  .Set(o => o.UpTrendDelta)
                  .Set(o => o.DownTrendDelta)
                );

        FuturesItiSignalMDI = Map(e => e.FuturesItiSignalMDI)
            .Parameters(e =>
                e.Set(o => o.ContractId)
                  .Set(o => o.ValueDate)
                  .Set(o => o.IntrinsicTime)
                  .Set(o => o.TrendType, o => o.AsEnum<IntrinsicTimeTrendType>())
                  .Set(o=>o.MDI)
            );

        FuturesOptionQuote = Map(e => e.FuturesOptionQuote)
            .Parameters(e =>
                e.Set(o => o.ContractId)
                  .Set(o => o.RequestId)
                  .Set(o => o.QuoteId, o => o.AsGuid())
                  .Set(o => o.ContractId)
                  .Set(o => o.CreatedBy)
                  .Set(o => o.CreatedOn)
            );

        FuturesOptionQuoteData = Map(e => e.FuturesOptionQuoteData)
            .Parameters(e =>
                e.Set(o => o.ContractId)
                  .Set(o => o.RequestId)
                  .Set(o => o.BidPrice)
                  .Set(o => o.BidSize)
                  .Set(o => o.AskPrice)
                  .Set(o => o.AskSize)
            );

    }

    /// <summary>
    /// return db reader/writer properties
    /// </summary>
    public IMarketDataDbReadContext DbReader => this;
    public IMarketDataDbWriteContext DbWriter => this;

    // map data objects...
    internal DbMap<NormalCurveDataReadModel> NormalCurveData { get; private set; }
    internal DbMap<MarketHolidayReadModel> MarketHoliday { get; private set; }
    internal DbMap<MarketExchangeReadModel> MarketExchange { get; private set; }
    internal DbMap<FuturesBarDataReadModel> FuturesBarData { get; private set; }
    internal DbMap<FuturesContractViewModel> FuturesContract { get; private set; }
    internal DbMap<FuturesOptionContractReadModel> FuturesOptionContract { get; private set; }
    internal DbMap<FuturesEodDataViewModel> FuturesEodData { get; private set; }
    internal DbMap<VixFuturesEodDataReadModel> VixFuturesEodData { get; private set; }
    internal DbMap<FuturesOptionTickDataViewModel> FuturesOptionTickData { get; private set; }
    internal DbMap<FuturesTickDataViewModel> FuturesTickData { get; private set; }
    internal DbMap<YieldCurveRateReadModel> YieldCurveRate { get; private set; }
    internal DbMap<RateOfReturnReadModel> ReturnRate { get; private set; }
    internal DbMap<MarketVolatilityStrikePriceOffsetReadModel> MarketVolatilityStrikePriceOffset { get; private set; }
    internal DbMap<TradeLiveFeedReadModel> TradeLiveFeed { get; private set; }
    internal DbMap<FuturesClosingPriceReadModel> FuturesClosingPrice { get; private set; }
    internal DbMap<FuturesTradeSignalViewModel> FuturesTradeSignal { get; private set; }
    internal DbMap<FuturesTradeSignalId> FuturesTradeSignalId { get; private set; }
    internal DbMap<FuturesRsiSignalReadModel> FuturesRsiSignal { get; private set; }
    internal DbMap<FuturesTdiSignalReadModel> FuturesTdiSignal { get; private set; }
    internal DbMap<FuturesTrendDirectionReadModel> FuturesTrendDirection { get; private set; }
    internal DbMap<FuturesTradeSignalLLMReadModel> FuturesTradeSignalLLM { get; private set; }
    internal DbMap<FuturesTradeSignalMetricsLLMReadModel> FuturesTradeSignalMetricsLLM { get; private set; }
    internal DbMap<FuturesItiSignalViewModel> FuturesItiSignal { get; private set; }
    internal DbMap<FuturesEodMovingAverageReadModel> FuturesEodMovingAverage { get; private set; }
    internal DbMap<FuturesEodClosingPriceReadModel> FuturesEodClosingPrice { get; private set; }
    internal DbMap<FuturesItiTrendDeltaDataReadModel> FuturesItiTrendDeltaData { get; private set; }
    internal DbMap<FuturesItiTrendDeltaModelReadModel> FuturesItiTrendDeltaModel { get; private set; }
    internal DbMap<FuturesItiTrendClassDataReadModel> FuturesItiTrendClassData { get; private set; }
    internal DbMap<FuturesItiTrendClassModelReadModel> FuturesItiTrendClassModel { get; private set; }
    internal DbMap<FuturesItiSignalAveragePredictedTrendDeltaDataModel> FuturesItiSignalAveragePredictedTrendDelta { get; private set; }
    internal DbMap<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel> FuturesItiSignalAveragePredictedTrendDeltaRange { get; private set; }
    internal DbMap<FuturesItiSignalAverageTrendDeltaRangeReadModel> FuturesItiSignalAverageTrendDeltaRange { get; private set; }
    internal DbMap<FuturesItiSignalAverageRSIReadModel> FuturesItiSignalAverageRSI { get; private set; }
    internal DbMap<(string Symbol, DateTime StartDate, DateTime EndDate, double UpTrendDelta, double DownTrendDelta)> FuturesItiSignalTrendDeltaByDirectionChanged { get; private set; }
    internal DbMap<FuturesItiSignalMDIViewModel> FuturesItiSignalMDI { get; private set; }
    internal DbMap<FuturesOptionQuoteReadModel> FuturesOptionQuote { get; private set; }
    internal DbMap<FuturesOptionQuoteDataReadModel> FuturesOptionQuoteData { get; private set; }

    public enum StoredProcedure
    {
        spBackupDatabase,
        spDeleteFuturesBarData,
        spDeleteFuturesContract,
        spDeleteFuturesOptionContract,
        spDeleteFuturesOptionQuotes,
        spDeleteFuturesOptionQuoteData,
        spDeleteFuturesItiTrendDeltaData,
        spDeleteFuturesItiTrendClassData,
        spDeleteStreamingRequestId,
        spDeleteYieldCurveRate,
        spDeleteTradeLiveFeed,
        spGetCurrentTradedFuturesContract,
        spGetCurrentlyTradedFuturesContracts,
        spGetFuturesBarData,
        spGetFuturesBarDataCount,
        spGetFuturesContract,
        spGetFuturesClosingPrice,
        spGetFuturesContractExists,
        spGetFuturesContracts,
        spGetFuturesOptionContract,
        spGetFuturesOptionContractExists,
        spGetFuturesOptionContracts,
        spGetFuturesOptionQuotes,
        spGetFuturesOptionQuoteData,
        spGetCurrentFuturesEodData,
        spGetFuturesEodData,
        spGetFuturesEodMovingAverage,
        spGetFuturesTrendDirectionFromRSISignal,
        spGetFuturesItiSignals,
        spGetFuturesItiSignalsByDateRange,
        spGetFuturesItiSignalAverageRSI,
        spGetFuturesItiSignalAverageMDI,
        spGetFuturesItiSignalAverageTrendDelta,
        spGetFuturesItiSignalAverageTrendDeltaByDateRange,
        spGetFuturesItiSignalAveragePredictedTrendDelta,
        spGetFuturesItiSignalAveragePredictedTrendDeltaByDateRange,
        spGetFuturesItiSignalMDI,
        spGetFuturesItiSignalMDIByTrend,
        spGetFuturesItiSignalTrendDeltaData,
        spGetFuturesItiSignalTrendDeltaByDirectionChanged,
        spGetFuturesItiSignalTrendClassData,
        spGetFuturesItiTrendDeltaData,
        spGetFuturesItiTrendClassData,
        spGetFuturesItiTrendDeltaModel,
        spGetFuturesItiTrendClassModel,
        spGetFuturesItiTrendDirectionChangedSignals,
        spGetVixFuturesEodData,
        spGetVixFuturesEodDataByValueDate,
        spGetCurrentFuturesEodDataByDateRange,
        spGetFuturesEodDataByDateRange,
        spGetFuturesEodClosingPrices,
        spGetFuturesTradeSignalIdByValueDate,
        spGetFuturesTradeSignalLLMByDateRange,
        spGetFuturesTradeSignalMetricsLLMByDateRange,
        spGetIndexContract,
        spGetLastFuturesTickData,
        spGetLastFuturesTickDataByTickDate,
        spGetLastFuturesOptionTickData,
        spGetLastFuturesRsiSignal,
        spGetLastFuturesTdiSignal,
        spGetLastFuturesTradeSignal,
        spGetLastFuturesTradeSignalBySymbol,
        spGetLastFuturesItiSignal,
        spGetLastFuturesItiSignalTrendExtremeChange,
        spGetLastFuturesItiSignalTrendDirectionChange,
        spGetLastFuturesItiSignalTrendReversalChange,
        spGetLastVixFuturesEodData,
        spGetLastVixFuturesEodDataBeforeValueDate,
        spGetMarketHolidays,
        spGetMarketExchanges,
        spGetMarketVolatilityStrikePriceOffsets,
        spGetNormalCurveData,
        spGetLastYieldCurveRate,
        spGetRateOfReturn,
        spGetLastRateOfReturn,
        spGetStreamingRequestId,
        spGetYieldCurveRate,
        spGetYieldCurveRates,
        spGetYieldCurveRateYears,
        spGetYieldCurveRateExists,
        spGetYesterdaysFuturesClosingPrice,
        spInsertFuturesContract,
        spInsertFuturesOptionContract,
        spInsertFuturesTickData,
        spInsertFuturesRsiSignal,
        spInsertFuturesTdiSignal,
        spInsertFuturesItiSignal,
        spInsertFuturesItiTrendDeltaData,
        spInsertFuturesItiTrendClassData,
        spInsertFuturesItiTrendDeltaModel,
        spInsertFuturesItiTrendClassModel,
        spInsertFuturesTrendDirection,
        spInsertFuturesTradeSignal,
        spInsertFuturesTradeSignalLLM,
        spInsertFuturesTradeSignalMetricsLLM,
        spInsertFuturesOptionTickData,
        spInsertFuturesOptionQuote,
        spInsertFuturesOptionQuoteData,
        spInsertFuturesBarData,
        spInsertFuturesEodData,
        spInsertFuturesClosingPrice,
        spInsertVixFuturesEodData,
        spInsertIndexContract,
        spInsertRateOfReturn,
        spInsertTradeLiveFeed,
        spInsertYieldCurveRate,
        spInsertStreamingDataLog,
        spUpdateFuturesEodDataNearestStrikes,
        spUpdateFuturesEodData,
        spUpdateFuturesContract,
        spUpdateFuturesOptionContract,
        spUpdateFuturesItiSignalActualThreshold,
        spUpdateFuturesItiSignalTrendExtreme,
        spUpdateFuturesItiSignalTrendReversal,
        spUpdateFuturesItiSignalIntrinsicTimeLength,
        spUpdateYieldCurveRate
    }

    /// <summary>
    /// delete futures bar data less than value date
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task DeleteFuturesBarDataAsync(FuturesBarDataId e)
    {
        var db = _dbFactory.MarketDataDb;
        await db.Use(StoredProcedure.spDeleteFuturesBarData)
            .SetParameters(new { 
                contractId = e.ContractId ,
                symbol = e.Symbol,
                valueDate = e.ValueDate,
            })
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// delete futures contract by id
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task DeleteFuturesContractAsync(string contractId)
    {
        var db = _dbFactory.MarketDataDb;
        await db .Use(StoredProcedure.spDeleteFuturesContract)
            .SetParameters(new { contractId })
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// delete futures option contract by id
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task DeleteFuturesOptionContractAsync(string contractId)
    {
        var db = _dbFactory.MarketDataDb;
        await db.Use(StoredProcedure.spDeleteFuturesOptionContract)
            .SetParameters(new { contractId })
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// delete futures option quotes and quote data by quote id
    /// </summary>
    /// <param name="quoteId"></param>
    /// <returns></returns>
    public async Task DeleteFuturesOptionQuotesAsync(int quoteId)
    {
        var db = _dbFactory.MarketDataDb;
        var futuresOptionQuotes = await db .Use(StoredProcedure.spGetFuturesOptionQuotes)
               .SetParameters(new { quoteId })
               .ExecuteQueryAsync<FuturesOptionQuoteReadModel>();

        // delete quote data..
        var queuedCommands = new List<object>();
        foreach (var e in futuresOptionQuotes)
            queuedCommands.Add(db.Use(StoredProcedure.spDeleteFuturesOptionQuoteData)
            .SetParameters(new { contractId = e.Id.ContractId, requestId = e.Id.RequestId })
            .QueueCommand());
        
        // delete all quotes by quote id...
        queuedCommands.Add(db.Use(StoredProcedure.spDeleteFuturesOptionQuotes)
            .SetParameters(new { quoteId = $"{quoteId}" })
            .QueueCommand());
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// delete streaming request id
    /// </summary>
    /// <param name="streamId"></param>
    /// <returns></returns>
    public async Task DeleteStreamingRequestIdAsync(int requestId)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spDeleteStreamingRequestId)
            .SetParameters(new { requestId })
            .ExecuteCommandAsync ();    

    /// <summary>
    /// delete yield curve rate by value date
    /// </summary>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task DeleteYieldCurveRateAsync(DateTime valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        await db.Use(StoredProcedure.spDeleteYieldCurveRate)
            .SetParameters(new { valueDate })
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// delete trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task DeleteTradeLiveFeedAsync(int orderId, int tradeId)
    {
        var db = _dbFactory.MarketDataDb;
        await db.Use(StoredProcedure.spDeleteTradeLiveFeed)
            .SetParameters(new {
                orderId,
                tradeId })
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// return futures bar data row count less han value date
    /// </summary>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<int> GetFuturesBarDataCountAsync(DateTime valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesBarDataCount)
             .SetParameters(new { valueDate })
             .ExecuteScalarAsync<int>("FuturesBarDataCount");
    }

    /// <summary>
    /// return futures iti signal average trend delta
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<double> GetFuturesItiSignalAverageTrendDeltaAsync(string contractId, DateTime valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesItiSignalAverageTrendDelta)
             .SetParameters(new { contractId, valueDate })
             .ExecuteScalarAsync<double>("AverageTrendDelta");
    }

    /// <summary>
    /// return stream request id
    /// </summary>
    /// <param name="streamId"></param>
    /// <returns></returns>
    public async Task<int> GetStreamingRequestIdAsync()
        =>  await _dbFactory.MarketDataDb
                .Use(StoredProcedure.spGetStreamingRequestId)
                .ExecuteScalarAsync<int>();

    /// <summary>
    /// return futures iti signal average predicted trend delta
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesItiSignalAveragePredictedTrendDeltaDataModel> GetFuturesItiSignalAveragePredictedTrendDeltaAsync(string contractId, DateTime valueDate)
    {
        var db = _dbFactory.MarketDataDb;
       return await db.Use(StoredProcedure.spGetFuturesItiSignalAveragePredictedTrendDelta)
             .SetParameters(new { contractId, valueDate })
             .ExecuteSingleAsync<FuturesItiSignalAveragePredictedTrendDeltaDataModel>();
    }


    /// <summary>
    /// return futures iti signal average RSI
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesItiSignalAverageRSIReadModel> GetFuturesItiSignalAverageRSIAsync(string contractId, DateTime valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesItiSignalAverageRSI)
             .SetParameters(new { contractId, valueDate })
             .ExecuteSingleAsync<FuturesItiSignalAverageRSIReadModel>();
    }

    /// <summary>
    /// return futures iti signal MDI
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<FuturesItiSignalMDIViewModel>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesItiSignalMDI)
             .SetParameters(new { contractId, valueDate })
             .ExecuteQueryAsync<FuturesItiSignalMDIViewModel>();
    }

    /// <summary>
    /// return futures iti signal MDI by trend
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<FuturesItiSignalMDIViewModel>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, int intrinsicTimeGroupId)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesItiSignalMDIByTrend)
             .SetParameters(new { contractId, valueDate, intrinsicTimeTrend = $"{intrinsicTimeTrend}", intrinsicTimeGroupId })
             .ExecuteQueryAsync<FuturesItiSignalMDIViewModel>();
    }

    /// <summary>
    /// return futures iti MDI Distribution
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesItiMDIDistributionReadModel> GetFuturesItiMDIDistributionAsync(string contractId, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb as IMarketDataDbReadContext;
        var futuresItiSignalMDIs = (await db.GetFuturesItiSignalMDIAsync(contractId, valueDate)).ToArray();
        return new FuturesItiMDIDistributionReadModel(futuresItiSignalMDIs);
    }

    /// <summary>
    /// return futures iti MDI Distribution by trend
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesItiMDIDistributionReadModel> GetFuturesItiMDIDistributionByTrendAsync(string contractId, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb as IMarketDataDbReadContext;
        var upTrendMDIs = await db.GetFuturesItiSignalMDIByTrendAsync(contractId, valueDate, IntrinsicTimeTrendType.UpTrend, 0);
        var downTrendMDIs = await db.GetFuturesItiSignalMDIByTrendAsync(contractId, valueDate, IntrinsicTimeTrendType.DownTrend, 0);
        return new FuturesItiMDIDistributionReadModel(upTrendMDIs.ToArray());
    }

    /// <summary>
    /// return futures iti signal average predicted trend delta by date range
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel> GetFuturesItiSignalAveragePredictedTrendDeltaRangeAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesItiSignalAveragePredictedTrendDeltaByDateRange)
             .SetParameters(new { symbol, startDate, endDate })
             .ExecuteSingleAsync<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel>();
    }

    /// <summary>
    /// return futures iti signal average predicted trend delta by date range
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<FuturesItiSignalAverageTrendDeltaRangeReadModel> GetFuturesItiSignalAverageTrendDeltaRangeAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesItiSignalAverageTrendDeltaByDateRange)
             .SetParameters(new { symbol, startDate, endDate })
             .ExecuteSingleAsync<FuturesItiSignalAverageTrendDeltaRangeReadModel>();
    }

    /// return yesterdays futures closing price
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<FuturesClosingPriceReadModel> GetYesterdaysFuturesClosingPriceAsync(FuturesDataId e)
    {
        var db = _dbFactory.MarketDataDb;
        return await _dbFactory.MarketDataDb
             .Use(StoredProcedure.spGetYesterdaysFuturesClosingPrice)
             .SetParameters(new {
                 contractId = e.ContractId,
                 valueDate = e.ValueDate })
             .ExecuteSingleAsync<FuturesClosingPriceReadModel>();
    }

    /// <summary>
    /// return futures closing price
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<FuturesClosingPriceReadModel> GetFuturesClosingPriceAsync(FuturesDataId e)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesClosingPrice)
             .SetParameters(new {
                 contractId = e.ContractId,
                 valueDate = e.ValueDate })
             .ExecuteSingleAsync<FuturesClosingPriceReadModel>();
    }

    /// <summary>
    /// return currently traded futures contracts 
    /// </summary>
    /// <returns></returns>
    public async Task<IReadOnlyList<FuturesBarDataReadModel>> GetFuturesBarDataAsync(
        string contractId,
        string symbol,
        DateTime valueDate,
        DateTime startDate,
        DateTime endDate)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesBarData)
            .SetParameters(new {
                contractId,
                symbol,
                valueDate,
                startDate,
                endDate })
            .ExecuteQueryAsync<FuturesBarDataReadModel>();
    }

    /// <summary>
    /// return currently traded futures contract 
    /// </summary>
    /// <returns></returns>
    public async Task<FuturesContractViewModel> GetCurrentTradedFuturesContractAsync()
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetCurrentTradedFuturesContract)
            .ExecuteSingleAsync<FuturesContractViewModel>();
    }

    /// <summary>
    /// return currently traded futures contracts 
    /// </summary>
    /// <returns></returns>
    public async Task<IReadOnlyList<FuturesContractViewModel>> GetCurrentlyTradedFuturesContractsAsync()
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetCurrentlyTradedFuturesContracts)
            .ExecuteQueryAsync<FuturesContractViewModel>();
    }

    /// <summary>
    /// return selected futures contract by id
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task<FuturesContractViewModel> GetFuturesContractAsync(string contractId)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesContract)
            .SetParameters(new { contractId })
            .ExecuteSingleAsync<FuturesContractViewModel>();
    }

    /// <summary>
    /// return selected futures contracts by symbol
    /// </summary>
    /// <returns></returns>
    public async Task<IReadOnlyList<FuturesContractViewModel>> GetFuturesContractsAsync()
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesContracts)
            .ExecuteQueryAsync<FuturesContractViewModel>();
    }

    /// <summary>
    /// return selected futures option contract by id
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task<FuturesOptionContractReadModel> GetFuturesOptionContractAsync(string contractId)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesOptionContract)
            .SetParameters(new { contractId })
            .ExecuteSingleAsync<FuturesOptionContractReadModel>();
    }

    /// <summary>
    /// return futures option contracts by symbol
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(string symbol)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetFuturesOptionContracts)
            .SetParameters(new { symbol })
            .ExecuteQueryAsync<FuturesOptionContractReadModel>();
    }

    /// <summary>
    /// return futures otion quotes
    /// </summary>
    /// <param name="quoteId"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<FuturesOptionQuoteReadModel>> GetFuturesOptionQuotesAsync(Guid quoteId)
        => await _dbFactory.MarketDataDb
                .Use(StoredProcedure.spGetFuturesOptionQuotes)
                .SetParameters(new { quoteId = $"{quoteId}"})
                .ExecuteQueryAsync<FuturesOptionQuoteReadModel>();

    /// <summary>
    /// return futures otion quote data by contract id
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<FuturesOptionQuoteDataReadModel> GetFuturesOptionQuoteDataAsync(FuturesOptionQuoteId e)
        =>  await _dbFactory.MarketDataDb
                .Use(StoredProcedure.spGetFuturesOptionQuoteData)
                .SetParameters(new { contractId = e.ContractId, requestId = e.RequestId })
                .ExecuteSingleAsync<FuturesOptionQuoteDataReadModel>();
    
    /// <summary>
    /// return vix futures eod by date range
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    public async Task<IReadOnlyList<VixFuturesEodDataReadModel>> GetVixFuturesEodDataAsync(string contractId, DateTime valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetVixFuturesEodData)
            .SetParameters(new {
                contractId,
                valueDate })
            .ExecuteQueryAsync<VixFuturesEodDataReadModel>();
    }

    /// <summary>
    /// return vix futures eod by date range
    /// </summary>
    /// <param name="valueDate"></param>
    public async Task<IReadOnlyList<VixFuturesEodDataReadModel>> GetVixFuturesEodDataByValueDateAsync(DateTime valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetVixFuturesEodDataByValueDate)
            .SetParameters(new { valueDate })
            .ExecuteQueryAsync<VixFuturesEodDataReadModel>();
    }

    /// <summary>
    /// return last vix futures eod 
    /// </summary>
    /// <param name="valueDate"></param>
    public async Task<VixFuturesEodDataReadModel> GetLastVixFuturesEodDataAsync(DateTime valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        return await db.Use(StoredProcedure.spGetLastVixFuturesEodData)
            .SetParameters(new { valueDate })
            .ExecuteSingleAsync<VixFuturesEodDataReadModel>();
    }

    /// <summary>
    /// return single futures eod data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="e"></param>
    public async Task<FuturesEodDataViewModel> GetFuturesEodDataAsync(string contractId, DateTime e)
    {
        var db = _dbFactory.MarketDataDb;
        var futuresEodData = await db.Use(StoredProcedure.spGetFuturesEodData)
                .SetParameters(new {
                   contractId,
                   valueDate = new DateTime(e.Year, e.Month, e.Day) })
               .ExecuteSingleAsync<FuturesEodDataViewModel>();
        if (futuresEodData  == null)
        {
            futuresEodData = await db.Use(StoredProcedure.spGetCurrentFuturesEodData)
                .SetParameters(new {  valueDate = new DateTime(e.Year, e.Month, e.Day) })
                .ExecuteSingleAsync<FuturesEodDataViewModel>();
        }
        return futuresEodData;
    }

    /// <summary>
    /// return single futures eod moving average
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public async Task<FuturesEodMovingAverageReadModel> GetFuturesEodMovingAverageAsync(string symbol, DateTime startDate, DateTime endDate)
    => await _dbFactory.MarketDataDb
                .Use(StoredProcedure.spGetFuturesEodMovingAverage)
                .SetParameters(new {
                    symbol,
                    startDate,
                    endDate })
               .ExecuteSingleAsync<FuturesEodMovingAverageReadModel>();

    /// <summary>
    /// return single futures eod data
    /// </summary>
    /// <param name="e"></param>
    public async Task<FuturesEodDataViewModel?> GetCurrentFuturesEodDataAsync(DateTime valueDate)
    {
        var futuresEodData = await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetCurrentFuturesEodData)
            .SetParameters(new { valueDate = valueDate })
            .ExecuteSingleAsync<FuturesEodDataViewModel>();
        return futuresEodData;
    }

    /// <summary>
    /// return collection of futures eod data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="sd"></param>
    /// <param name="ed"></param>
    public async Task<IReadOnlyList<FuturesEodDataViewModel>> GetFuturesEodDataByDateRangeAsync(string contractId, DateTime sd, DateTime ed)
    {
        var db = _dbFactory.MarketDataDb;
        var futuresEodData = new List<FuturesEodDataViewModel>();
        futuresEodData.AddRange(await db
                .Use(StoredProcedure.spGetFuturesEodDataByDateRange)
                  .SetParameters(new {
                      contractId,
                      startDate = new DateTime(sd.Year, sd.Month, sd.Day),
                      endDate = new DateTime(ed.Year, ed.Month, ed.Day) })
                  .ExecuteQueryAsync<FuturesEodDataViewModel>());
        futuresEodData.AddRange(await db
                .Use(StoredProcedure.spGetCurrentFuturesEodDataByDateRange)
                   .SetParameters(new {
                       startDate = new DateTime(sd.Year, sd.Month, sd.Day),
                       endDate = new DateTime(ed.Year, ed.Month, ed.Day) })
                   .ExecuteQueryAsync<FuturesEodDataViewModel>());
        return futuresEodData
            .Distinct(new FuturesEodDataComparer())
            .OrderByDescending(e => e.ValueDate)
            .ToList(); ;
    }


    /// <summary>
    /// return last updated futures tick data
    /// </summary>
    /// <param name="contractId"></param>
    public async Task<FuturesTickDataViewModel> GetLastFuturesTickDataAsync(string contractId)
        => (await _dbFactory.MarketDataDb
                .Use(StoredProcedure.spGetLastFuturesTickData)
                .SetParameters(new { contractId })
                .ExecuteQueryAsync<FuturesTickDataViewModel>()).LastOrDefault();

    /// <summary>
    /// return last updated futures tick data by tick date
    /// </summary>
    /// <param name="contractId"></param>
    public async Task<FuturesTickDataViewModel> GetLastFuturesTickDataByTickDateAsync(string contractId, DateTime tickDate)
        => (await _dbFactory.MarketDataDb
                .Use(StoredProcedure.spGetLastFuturesTickDataByTickDate)
                .SetParameters(new { contractId, tickDate })
                .ExecuteQueryAsync<FuturesTickDataViewModel>()).LastOrDefault();

    /// <summary>
    /// return last updated futures option tick data
    /// </summary>
    /// <param name="contractId"></param>
    public async Task<FuturesOptionTickDataViewModel> GetLastFuturesOptionTickDataAsync(string contractId)
        => (await _dbFactory.MarketDataDb
             .Use(StoredProcedure.spGetLastFuturesOptionTickData)
             .SetParameters(new { contractId })
             .ExecuteQueryAsync<FuturesOptionTickDataViewModel>()).LastOrDefault();
             

    /// <summary>
    /// return last updated yield curve rate
    /// </summary>
    public async Task<YieldCurveRateReadModel> GetLastYieldCurveRateAsync()
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetLastYieldCurveRate)
            .ExecuteSingleAsync<YieldCurveRateReadModel>();

    /// <summary>
    /// return last futures trade signal
    /// </summary>
    public async Task<FuturesTradeSignalViewModel> GetLastFuturesTradeSignalAsync(string contractId, DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetLastFuturesTradeSignal)
            .SetParameters(new { contractId, valueDate })
            .ExecuteSingleAsync<FuturesTradeSignalViewModel>();

    /// <summary>
    /// return last futures trade signal by symbol
    /// </summary>
    public async Task<FuturesTradeSignalViewModel> GetLastFuturesTradeSignalBySymbolAsync(string symbol, DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetLastFuturesTradeSignalBySymbol)
            .SetParameters(new { symbol, valueDate })
            .ExecuteSingleAsync<FuturesTradeSignalViewModel>();

    /// <summary>
    /// return list of futures trade signal ids by value date
    /// </summary>
    public async Task<IReadOnlyList<FuturesTradeSignalId>> GetFuturesTradeSignalIdByValueDateAsync(DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesTradeSignalIdByValueDate)
            .SetParameters(new { valueDate = valueDate.Date})
            .ExecuteQueryAsync<FuturesTradeSignalId>();

    /// <summary>
    /// return list of futures trade signal LLM by date range
    /// </summary>
    public async Task<IReadOnlyList<FuturesTradeSignalLLMReadModel>> GetFuturesTradeSignalLLMByDateRangeAsync(string contractId, DateTime startDate, DateTime endDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesTradeSignalLLMByDateRange)
            .SetParameters(new
            {
                contractId,
                startDate = startDate.Date,
                endDate = endDate.Date
            })
            .ExecuteQueryAsync<FuturesTradeSignalLLMReadModel>();

    /// <summary>
    /// return list of futures trade signal metrics LLM by date range
    /// </summary>
    public async Task<IReadOnlyList<FuturesTradeSignalMetricsLLMReadModel>> GetFuturesTradeSignalMetricsLLMByDateRangeAsync(string contractId, DateTime startDate, DateTime endDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesTradeSignalMetricsLLMByDateRange)
            .SetParameters(new
            {
                contractId,
                startDate = startDate.Date,
                endDate = endDate.Date
            })
            .ExecuteQueryAsync<FuturesTradeSignalMetricsLLMReadModel>();

    /// <summary>
    /// return list of futures iti signal by value date
    /// </summary>
    public async Task<IReadOnlyList<FuturesItiSignalViewModel>> GetFuturesItiSignalsAsync(string contractId, DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesItiSignals)
            .SetParameters(new{
                contractId,
                valueDate = valueDate.Date})
            .ExecuteQueryAsync<FuturesItiSignalViewModel>();

    /// <summary>
    /// return list of futures iti signal by date range
    /// </summary>
    public async Task<IReadOnlyList<FuturesItiSignalViewModel>> GetFuturesItiSignalsAsync(string symbol, DateTime startDate, DateTime endDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesItiSignalsByDateRange)
            .SetParameters(new {
                symbol,
                startDate,
                endDate
            })
            .ExecuteQueryAsync<FuturesItiSignalViewModel>();

    /// <summary>
    /// return list of futures iti trend direction changed signals
    /// </summary>
    public async Task<IReadOnlyList<FuturesItiSignalViewModel>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesItiTrendDirectionChangedSignals)
            .SetParameters(new {
                contractId,
                valueDate = valueDate.Date })
            .ExecuteQueryAsync<FuturesItiSignalViewModel>();

    /// <summary>
    /// return futures trend direction
    /// from rsi signal
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="timestamp"></param>
    /// <param name="lookbackInterval"></param>
    /// <param name="startTime"></param>
    /// <param name="endTime"></param>
    /// <returns></returns>
    public async Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(string contractId, DateTime valueDate, DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime)
         => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesTrendDirectionFromRSISignal)
            .SetParameters(new { contractId, valueDate, timestamp, lookbackInterval, startTime, endTime})
            .ExecuteSingleAsync<FuturesTrendDirectionReadModel>();

    /// <summary>
    /// return yield curve rate by value date
    /// </summary>
    /// <param name="valueDate"></param>
    public async Task<YieldCurveRateReadModel> GetYieldCurveRateAsync(DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetYieldCurveRates)
            .SetParameters(new { valueDate })
            .ExecuteSingleAsync<YieldCurveRateReadModel>();

    /// <summary>
    /// return yield curve rates by date range
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public async Task<IReadOnlyList<YieldCurveRateReadModel>> GetYieldCurveRatesAsync(DateTime startDate, DateTime endDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetYieldCurveRates)
            .SetParameters(new { 
                startDate, 
                endDate })
            .ExecuteQueryAsync<YieldCurveRateReadModel>();

    /// <summary>
    /// return distinct list of years
    /// </summary>
    /// <returns></returns>
    public async Task<IReadOnlyList<int>> GetYieldCurveRateYearsAsync()
       => await _dbFactory.MarketDataDb
           .Use(StoredProcedure.spGetYieldCurveRateYears)
           .ExecuteQueryAsync<int>(e => e.Get("ValueDateYear").As<int>());

    /// <summary>
    /// return last updated rate of return
    /// </summary>
    /// <param name="symbol">security tick symbol</param>
    public async Task<RateOfReturnReadModel> GetLastRateOfReturnAsync(string symbol)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetLastRateOfReturn)
            .SetParameters(new { symbol })
            .ExecuteSingleAsync<RateOfReturnReadModel>();

    /// <summary>
    /// return last updated rate of return
    /// </summary>
    /// <param name="symbol">security tick symbol</param>
    /// <param name="valueDate"></param>
    public async Task<RateOfReturnReadModel> GetRateOfReturnAsync(string symbol, DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetRateOfReturn)
            .SetParameters(new { 
                symbol, 
                valueDate })
            .ExecuteSingleAsync<RateOfReturnReadModel>();

    /// <summary>
    /// return market exchanges
    /// </summary>
    /// <returns></returns>
    public async Task<IReadOnlyList<MarketExchangeReadModel>> GetMarketExchangesAsync()
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetMarketExchanges)
            .ExecuteQueryAsync<MarketExchangeReadModel>();

    /// <summary>
    /// return market volatility strike price offsets
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<MarketVolatilityStrikePriceOffsetReadModel>> GetMarketVolatilityStrikePriceOffsetsAsync(string symbol)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetMarketVolatilityStrikePriceOffsets)
            .SetParameters(new { symbol })
            .ExecuteQueryAsync<MarketVolatilityStrikePriceOffsetReadModel>();

    /// <summary>
    /// return futures eod closing price by date range over a symbol
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="maxDays"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<FuturesEodClosingPriceReadModel>> GetFuturesEodClosingPricesAsync(string symbol, DateTime startDate, DateTime endDate, int maxDays)
         => await _dbFactory.MarketDataDb
                .Use(StoredProcedure.spGetFuturesEodClosingPrices)
                .SetParameters(new { symbol, startDate, endDate, maxDays })
                .ExecuteQueryAsync<FuturesEodClosingPriceReadModel>();

    /// <summary>
    /// return last futures rsi signal
    /// </summary>
    public async Task<FuturesRsiSignalReadModel> GetLastFuturesRsiSignalAsync(string contractId, DateTime valueDate, FuturesRsiSignalType signalType)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetLastFuturesRsiSignal)
            .SetParameters(new { contractId, valueDate , signalType = $"{signalType}"})
            .ExecuteSingleAsync<FuturesRsiSignalReadModel>();

    /// <summary>
    /// return last futures tdi signal
    /// </summary>
    public async Task<FuturesTdiSignalReadModel> GetLastFuturesTdiSignalAsync(string contractId, DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetLastFuturesTdiSignal)
            .SetParameters(new { contractId, valueDate })
            .ExecuteSingleAsync<FuturesTdiSignalReadModel>();

    /// <summary>
    /// return last futures initrinsic time indicator signal
    /// </summary>
    public async Task<FuturesItiSignalViewModel> GetLastFuturesItiSignalAsync(string contractId, DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetLastFuturesItiSignal)
            .SetParameters(new { contractId, valueDate })
            .ExecuteSingleAsync<FuturesItiSignalViewModel>();

    /// <summary>
    /// return last futures initrinsic time indicator signal from tremd direction change
    /// </summary>
    public async Task<FuturesItiSignalViewModel> GetLastFuturesItiSignalTrendDirectionChangeAsync(string contractId, DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetLastFuturesItiSignalTrendDirectionChange)
            .SetParameters(new { contractId, valueDate })
            .ExecuteSingleAsync<FuturesItiSignalViewModel>();

    /// <summary>
    /// return last futures initrinsic time indicator signal from trend extreme change
    /// </summary>
    public async Task<FuturesItiSignalViewModel> GetLastFuturesItiSignalTrendExtremeChangeAsync(string contractId, DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetLastFuturesItiSignalTrendExtremeChange)
            .SetParameters(new { contractId, valueDate })
            .ExecuteSingleAsync<FuturesItiSignalViewModel>();

    /// <summary>
    /// return last futures initrinsic time indicator signal from trend reversal change
    /// </summary>
    public async Task<FuturesItiSignalViewModel> GetLastFuturesItiSignalTrendReversalChangeAsync(string contractId, DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetLastFuturesItiSignalTrendReversalChange)
            .SetParameters(new { contractId, valueDate })
            .ExecuteSingleAsync<FuturesItiSignalViewModel>();

    /// <summary>
    /// return futures iti signal by date range to populate trend delta data
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public async Task<IReadOnlyList<FuturesItiSignalViewModel>> GetFuturesItiSignalTrendDeltaDataAsync(string symbol, DateTime startDate, DateTime endDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesItiSignalTrendDeltaData)
            .SetParameters(new { 
                symbol, 
                startDate = startDate.Date,
                endDate = endDate.Date })
            .ExecuteQueryAsync<FuturesItiSignalViewModel>();

    //using TrendDeltaByDirectionChanged = (string Symbol, DateTime StartDate, DateTime EndDate, double UpTrendDelta, double DownTrendDelta);

    /// <summary>
    /// return futures iti signal by date range to populate trend delta data
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public async Task<(string Symbol, DateTime StartDate, DateTime EndDate, double UpTrendDelta, double DownTrendDelta)> 
        GetFuturesItiSignalTrendDeltaByDirectionChangedAsync(string symbol, DateTime startDate, DateTime endDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesItiSignalTrendDeltaByDirectionChanged)
            .SetParameters(new
            {
                symbol,
                startDate = startDate.Date,
                endDate = endDate.Date
            })
            .ExecuteSingleAsync<(string Symbol, DateTime StartDate, DateTime EndDate, double UpTrendDelta, double DownTrendDelta)>();


    /// <summary>
    ///  return futures iti signal by date range to populate trend class data
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<FuturesItiSignalViewModel>> GetFuturesItiSignalTrendClassDataAsync(string symbol, DateTime startDate, DateTime endDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesItiSignalTrendClassData)
            .SetParameters(new
            {
                symbol,
                startDate = startDate.Date,
                endDate = endDate.Date
            })
            .ExecuteQueryAsync<FuturesItiSignalViewModel>();

    /// <summary>
    /// return futures iti trend delta data by date range
    /// </summary>
    public async Task<IReadOnlyList<FuturesItiTrendDeltaDataReadModel>> GetFuturesItiTrendDeltaDataAsync(string symbol, DateTime startDate, DateTime endDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesItiTrendDeltaData)
            .SetParameters(new {
                symbol,
                startDate = startDate.Date,
                endDate = endDate.Date })
            .ExecuteImmutableQueryAsync<FuturesItiTrendDeltaDataReadModel>();

    /// <summary>
    /// return futures iti trend class data by date range
    /// </summary>
    public async Task<IReadOnlyList<FuturesItiTrendClassDataReadModel>> GetFuturesItiTrendClassDataAsync(string symbol, DateTime startDate, DateTime endDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesItiTrendClassData)
            .SetParameters(new
            {
                symbol,
                startDate = startDate.Date,
                endDate = endDate.Date
            })
            .ExecuteImmutableQueryAsync<FuturesItiTrendClassDataReadModel>();

    /// <summary>
    /// return futures iti trend delta model
    /// </summary>
    public async Task<FuturesItiTrendDeltaModelReadModel> GetFuturesItiTrendDeltaModelAsync(string symbol, DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesItiTrendDeltaModel)
            .SetParameters(new {
                symbol,
                valueDate = valueDate.Date })
            .ExecuteSingleAsync<FuturesItiTrendDeltaModelReadModel>();

    /// <summary>
    /// return futures iti trend delta model
    /// </summary>
    public async Task<FuturesItiTrendClassModelReadModel> GetFuturesItiTrendClassModelAsync(string symbol, DateTime valueDate)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetFuturesItiTrendClassModel)
            .SetParameters(new
            {
                symbol,
                valueDate = valueDate.Date
            })
            .ExecuteSingleAsync<FuturesItiTrendClassModelReadModel>();


    /// <summary>
    /// insert futures option contract
    /// </summary>
    /// <param name="e">futures option contract</param>
    /// <returns></returns>
    public async Task InsertFuturesContractAsync(FuturesContractViewModel e)
       => await _dbFactory.MarketDataDb
           .Use(StoredProcedure.spInsertFuturesContract)
           .SetParameters(new {
               contractId = e.ContractId,
               description = e.Description,
               symbol = e.Symbol,
               localSymbol = e.LocalSymbol,
               securityType = e.SecurityType,
               currency = e.Currency,
               exchange = e.Exchange,
               multiplier = e.Multiplier,
               lastTradeDate = e.LastTradeDate.AsLocalDate(),
               currentlyTraded = e.CurrentlyTraded })
           .ExecuteCommandAsync();

    public async Task ReplaceFuturesContractAsync(string contractId, FuturesContractViewModel e)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.MarketDataDb;
        queuedCommands.Add( db.Use(StoredProcedure.spDeleteFuturesContract)
            .SetParameters(new { contractId })
            .QueueCommand());
        queuedCommands.Add(db.Use(StoredProcedure.spInsertFuturesContract)
            .SetParameters(new {
                 contractId = e.ContractId,
                 description = e.Description,
                 symbol = e.Symbol,
                 localSymbol = e.LocalSymbol,
                 securityType = e.SecurityType,
                 currency = e.Currency,
                 exchange = e.Exchange,
                 multiplier = e.Multiplier,
                 lastTradeDate = e.LastTradeDate,
                 currentlyTraded = e.CurrentlyTraded })
            .QueueCommand());
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// replace futures option contract
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ReplaceFuturesOptionContractAsync(string contractId, FuturesOptionContractReadModel e)
    {
        var queuedCommands = new List<object>();    
        var db = _dbFactory.MarketDataDb;
        queuedCommands.Add(db.Use(StoredProcedure.spDeleteFuturesOptionContract)
            .SetParameters(new { contractId })
            .QueueCommand());

        queuedCommands.Add(db.Use(StoredProcedure.spInsertFuturesOptionContract)
            .SetParameters(new {
                contractId = e.ContractId,
                description = e.Description ?? string.Empty,
                symbol = e.Symbol,
                localSymbol = e.LocalSymbol,
                securityType = e.SecurityType,
                currency = e.Currency,
                exchange = e.Exchange,
                multiplier = e.Multiplier,
                contractMonth = e.ContractMonth,
                strikePrice = e.StrikePrice,
                optionType = e.OptionType })
            .QueueCommand());

        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// insert futures option contract
    /// </summary>
    /// <param name="e">future option contract</param>
    /// <returns></returns>
    public async Task InsertFuturesOptionContractAsync(FuturesOptionContractReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertFuturesOptionContract)
            .SetParameters(new {
                contractId = e.ContractId,
                description = e.Description ?? string.Empty,
                symbol = e.Symbol,
                localSymbol = e.LocalSymbol,
                securityType = e.SecurityType,
                currency = e.Currency,
                exchange = e.Exchange,
                multiplier = e.Multiplier,
                contractMonth = e.ContractMonth,
                strikePrice = e.StrikePrice,
                optionType = e.OptionType })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert futures option contracts
    /// </summary>
    /// <param name="e">future option contracts</param>
    /// <returns></returns>
    public async Task InsertFuturesOptionContractsAsync(FuturesOptionContractReadModel[] contracts)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.MarketDataDb;
        foreach (var e in contracts)
        {
            // only insert futures option contract if contract does not exist...
            if (! await db.Use(StoredProcedure.spGetFuturesContractExists)
                    .SetParameters(new { e.ContractId })
                    .ExecuteSingleAsync<bool>(o => o.Get("FuturesContractExists").As<bool>()) )
            {
                queuedCommands.Add(db.Use(StoredProcedure.spInsertFuturesOptionContract)
                .SetParameters(new
                {
                    contractId = e.ContractId,
                    description = e.Description ?? string.Empty,
                    symbol = e.Symbol,
                    localSymbol = e.LocalSymbol,
                    securityType = e.SecurityType,
                    currency = e.Currency,
                    exchange = e.Exchange,
                    multiplier = e.Multiplier,
                    contractMonth = e.ContractMonth,
                    strikePrice = e.StrikePrice,
                    optionType = e.OptionType
                })
                .QueueCommand());
            }
        }
        await db.ExecuteQueuedCommandsAsync(queuedCommands  );
    }

    /// <summary>
    /// return all normal curve data
    /// </summary>
    public async Task<IReadOnlyList<NormalCurveDataReadModel>> GetNormalCurveDataAsync()
        => await _dbFactory.MarketDataDb
            .Use(MarketDataDbCql.GetNormalCurveData)
            .ExecuteQueryAsync<NormalCurveDataReadModel>(e => new NormalCurveDataReadModel(
                Index: e.Get("StdDevIndex").As<double>(),
                Percent: e.Get("StdDevPercent").As<double>()));

    /// <summary>
    /// return normal curve table
    /// </summary>
    /// <returns></returns>
    public async Task<NormalCurveTableReadModel> GetNormalCurveTableAsync()
    {
        _normalCurveTable ??= new NormalCurveTableReadModel(
               NormalCurveTable: (await GetNormalCurveDataAsync().ConfigureAwait(false)).ToArray());
        return _normalCurveTable;
    }

    /// <summary>
    /// insert futures trade signal into storage
    /// </summary>
    /// <param name="e"></param>
    public async Task InsertFuturesTradeSignalAsync(FuturesTradeSignalViewModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertFuturesTradeSignal)
            .SetParameters(new {
                contractId = e.ContractId,
                valueDate = e.ValueDate,
                mean = e.Mean,
                stdDev = e.StdDev,
                futuresPrice = e.FuturesPrice,
                priceChangePercent = e.PriceChangePercent,
                fundRiskPercent = e.FundRiskPercent,
                rsi = e.RSI,
                rsiSlope = e.RSISlope,
                trendType = $"{e.TrendType}",
                trendStrength = $"{e.TrendStrength}",
                tradeSignal = $"{e.TradeSignal}",
                tdi = $"{e.TDI}",
                tdiStrength = $"{e.TDIStrength}",
                mdi = e.MDI,
                mdiTrend = $"{e.MDITrend}",
                mdiUpTrendLimit = e.MDIUpTrendLimit,
                mdiDownTrendLimit = e.MDIDownTrendLimit,
                upTrendingTrigger = e.UpTrendingTrigger,
                downTrendingTrigger = e.DownTrendingTrigger,
                entryTrigger = e.EntryTrigger,
                exitTrigger = e.ExitTrigger,
                trendDelta = e.TrendDelta,
                trendExtreme = e.TrendExtreme,
                trendReversal = e.TrendReversal,
                fiftyDma = e.FiftyDMA,
                twoHundredDma = e.TwoHundredDMA,
                tradeExecuteState = $"{e.TradeExecuteState}",
                createdOn = e.CreatedOn,
                createdBy = e.CreatedBy })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert futures trade signal into storage
    /// </summary>
    /// <param name="e"></param>
    public async Task InsertFuturesTradeSignalLLMAsync(FuturesTradeSignalLLMReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertFuturesTradeSignalLLM)
            .SetParameters(new
            {
                contractId = e.ContractId,
                valueDate = e.ValueDate,
                timestamp = e.Timestamp,
                openPrice = e.OpenPrice,
                highPrice = e.HighPrice,
                lowPrice = e.LowPrice,
                closePrice = e.ClosePrice,
                volume = e.Volume,
                dailyPercentChange = e.DailyPercentChange,
                dailyStdDev = e.DailyStdDev,
                upperBand = e.UpperBand,
                mean = e.Mean,
                lowerBand = e.LowerBand,
                priceVolatility = e.PriceVolatility,
                createdOn = e.CreatedOn,
                createdBy = e.CreatedBy
            })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert futures trade signal metrics LLM into storage
    /// </summary>
    /// <param name="e"></param>
    public async Task InsertFuturesTradeSignalMetricsLLMAsync(FuturesTradeSignalMetricsLLMReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertFuturesTradeSignalMetricsLLM)
            .SetParameters(new
            {
                contractId = e.ContractId,
                valueDate = e.ValueDate,
                timestamp = e.Timestamp,
                marketDirection = $"{e.MarketDirection}",
                marketVolatility = $"{e.MarketVolatility}",
                priceDirection = $"{e.PriceDirection}",
                priceVolatility = $"{e.PriceVolatility}",
                marketDirectionIndicator = e.MarketDirectionIndicator,
                createdOn = e.CreatedOn,
                createdBy = e.CreatedBy
            })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert funtures iti trend delta model
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFuturesItiTrendDeltaModelAsync(FuturesItiTrendDeltaModelReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertFuturesItiTrendDeltaModel)
            .SetParameters(new
            {
                symbol = e.Symbol,
                valueDate = e.ValueDate,
                startDate = e.StartDate,
                endDate = e.EndDate,
                count = e.Count,
                maximum = e.Maximum,
                mean = e.Mean,
                median = e.Median,
                minimum = e.Minimum,
                skewness = e.Skewness,
                stdDev = e.StdDev,
                variance = e.Variance,
                meanAbsoluteError = e.MeanAbsoluteError,
                meanSquaredError = e.MeanSquaredError,
                rootMeanSquaredError = e.RootMeanSquaredError,
                lossFunction = e.LossFunction,
                rSquared = e.RSquared,
                modelData = e.ModelData
            })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert funtures iti trend class model
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFuturesItiTrendClassModelAsync(FuturesItiTrendClassModelReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertFuturesItiTrendClassModel)
            .SetParameters(new
            {
                symbol = e.Symbol,
                valueDate = e.ValueDate,
                startDate = e.StartDate,
                endDate = e.EndDate,
                count = e.Count,
                maximum = e.Maximum,
                mean = e.Mean,
                median = e.Median,
                minimum = e.Minimum,
                skewness = e.Skewness,
                stdDev = e.StdDev,
                variance = e.Variance,
                accuracy = e.Accuracy,
                areaUnderPrecisionRecallCurve = e.AreaUnderPrecisionRecallCurve,
                areaUnderRocCurve = e.AreaUnderRocCurve,
                entropy = e.Entropy,
                f1Score = e.F1Score,
                modelData = e.ModelData
            })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert futures iti signal into storage
    /// </summary>
    /// <param name="e"></param>
    public async Task InsertFuturesItiSignalAsync(FuturesItiSignalViewModel e)
    {
        var db = _dbFactory.MarketDataDb;
        var tradeState = e.TradeState;
        switch (e.IntrinsicTimeMode)
        {
            case IntrinsicTimeModeType.TrendExtremeChanged:
                await db.Use(StoredProcedure.spUpdateFuturesItiSignalTrendExtreme)
                    .SetParameters(new {
                        contractId = e.ContractId,
                        valueDate = e.ValueDate,
                        intrinsicTimeMode = $"{IntrinsicTimeModeType.TrendDirectionChanged}",
                        trendExtreme = e.TrendExtreme })
                    .ExecuteCommandAsync();
                break;
            case IntrinsicTimeModeType.TrendReversalChanged:
                await db.Use(StoredProcedure.spUpdateFuturesItiSignalTrendReversal)
                    .SetParameters(new
                    {
                        contractId = e.ContractId,
                        valueDate = e.ValueDate,
                        intrinsicTimeMode = $"{IntrinsicTimeModeType.TrendDirectionChanged}",
                        trendReversal = e.TrendReversal
                    })
                    .ExecuteCommandAsync();
                break;
            case IntrinsicTimeModeType.TrendDirectionChanged:
                var lastTrendDirectionChange = await db.Use(StoredProcedure.spGetLastFuturesItiSignalTrendDirectionChange)
                    .SetParameters(new { e.ContractId, e.ValueDate })
                    .ExecuteSingleAsync<FuturesItiSignalViewModel>();
                if (lastTrendDirectionChange is not null)
                {
                    var trendDirectionDiff = e.IntrinsicTime - lastTrendDirectionChange.IntrinsicTime;
                    await db.Use(StoredProcedure.spUpdateFuturesItiSignalIntrinsicTimeLength)
                        .SetParameters(new  {
                            contractId = e.ContractId,
                            valueDate = e.ValueDate,
                            intrinsicTimeMode = $"{IntrinsicTimeModeType.TrendDirectionChanged}",
                            intrinsicTimeLength = trendDirectionDiff.TotalSeconds,
                            trendDelta = e.TrendExtreme - e.TrendPrice
                        })
                        .ExecuteCommandAsync();
                    if (tradeState == IntrinsicTimeTradeState.Ready)
                        tradeState = trendDirectionDiff.TotalMinutes < 5
                            ? IntrinsicTimeTradeState.Hold
                            : IntrinsicTimeTradeState.Ready;
                }
                break;
        }
        await db.Use(StoredProcedure.spInsertFuturesItiSignal)
            .SetParameters(new {
                contractId = e.ContractId,
                valueDate = e.ValueDate,
                intrinsicTime = e.IntrinsicTime,
                intrinsicTimeGroupId = e.IntrinsicTimeGroupId,
                intrinsicTimeLength = e.IntrinsicTimeLength,
                intrinsicPrice = e.IntrinsicPrice,
                intrinsicTimeTrend = $"{e.IntrinsicTimeTrend}",
                intrinsicTimeMode = $"{e.IntrinsicTimeMode}",
                trendPrice = e.TrendPrice,
                trendExtreme = e.TrendExtreme,
                trendReversal = e.TrendReversal,
                lambda = e.Lambda,
                targetDelta = e.TargetDelta,
                predictedDelta = e.PredictedDelta,
                trendDelta = e.TrendDelta,
                upTrendTrigger = e.UpTrendTrigger,
                downTrendTrigger = e.DownTrendTrigger,
                futuresPercentChange = e.FuturesPercentChange,
                futuresMean = e.FuturesMean,
                futuresStdDev = e.FuturesStdDev,
                futuresMDI = e.FuturesMDI,
                futuresMDITrend = $"{e.FuturesMDITrend}",
                futuresMDIUpTrendLimit = e.FuturesMDIUpTrendLimit,
                futuresMDIDownTrendLimit = e.FuturesMDIDownTrendLimit,
                futuresRSI = e.FuturesRSI,
                futuresRSISlope = e.FuturesRSISlope,
                futuresFiftyDMA = e.FuturesFiftyDMA,
                futuresTwoHundredDMA = e.FuturesTwoHundredDMA,
                tradeState = $"{tradeState}",
                upTrendCoastLineCounter = e.UpTrendCoastLineCounter,
                downTrendCoastLineCounter = e.DownTrendCoastLineCounter  })
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// load futures iti trend deltadata by date range into
    /// </summary>
    /// <param name="e"></param>
    public async Task<FuturesItiTrendModelDataStatistics> LoadFuturesItiTrendDeltaDataAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        var trendDeltaDataStats = new FuturesItiTrendModelDataStatistics();
        var db = _dbFactory.MarketDataDb;
        var futuresItiSignals = await db .Use(StoredProcedure.spGetFuturesItiSignalTrendDeltaData)
            .SetParameters(new { symbol, startDate, endDate })
            .ExecuteQueryAsync<FuturesItiSignalViewModel>();
        if ((futuresItiSignals?.Count ?? 0) > 0)
        {
            var queuedCommands = new List<object>();    
            queuedCommands.Add(db.Use(StoredProcedure.spDeleteFuturesItiTrendDeltaData)
              .SetParameters(new {symbol })
              .QueueCommand());

            foreach (var e in futuresItiSignals)
            {
                queuedCommands.Add(db.Use(StoredProcedure.spInsertFuturesItiTrendDeltaData)
               .SetParameters(new {
                   symbol,
                   valueDate = e.ValueDate,
                   timestamp = e.IntrinsicTime,
                   //trendDelta = e.IntrinsicTimeMode == IntrinsicTimeModeType.TrendReversalChanged ? e.TrendReversal - e.TrendPrice : e.TrendExtreme - e.TrendPrice,
                   trendDelta = e.IntrinsicTimeMode == IntrinsicTimeModeType.TrendDirectionChanged ? e.TrendExtreme - e.TrendPrice : e.TrendExtreme - e.IntrinsicPrice,
                   trendDirection = e.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend ? 1 : 0,
                   trendDirectionMode = GetTrendDirectionMode(e.IntrinsicTimeMode),
                   //futuresPrice = e.TrendPrice,
                   //trendExtreme = e.IntrinsicTimeMode == IntrinsicTimeModeType.TrendReversalChanged ? e.TrendReversal: e.TrendExtreme,
                   futuresPrice = e.IntrinsicPrice,
                   trendExtreme = e.TrendExtreme,
                   futuresRSI = e.FuturesRSI == -1 ? 50 : e.FuturesRSI,
               })
               .QueueCommand());
            }
            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }
        if ((futuresItiSignals?.Count ?? 0) > 0)
        {
            var nd = Normal.Estimate(futuresItiSignals.Select(e => e.TrendDelta));
            trendDeltaDataStats = new FuturesItiTrendModelDataStatistics(
                Count: futuresItiSignals.Count,
                Maximum: futuresItiSignals.Max(e => e.TrendDelta),
                Mean: nd.Mean,
                Median: nd.Median,
                Minimum: futuresItiSignals.Min(e => e.TrendDelta),
                Skewness: nd.Skewness,
                StdDev: nd.StdDev,
                Variance: nd.Variance);
        }
        return trendDeltaDataStats;

        int GetTrendDirectionMode(IntrinsicTimeModeType e)
            => e switch {
                IntrinsicTimeModeType.TrendDirectionChanged => 0,
                IntrinsicTimeModeType.TrendExtremeChanged => 1,
                IntrinsicTimeModeType.TrendReversalChanged => -1,
                _ => 0
            };

     }

    /// <summary>
    /// load futures iti trend class  data by date range into
    /// </summary>
    /// <param name="e"></param>
    public async Task<FuturesItiTrendModelDataStatistics> LoadFuturesItiTrendClassDataAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        var trendDeltaDataStats = new FuturesItiTrendModelDataStatistics();
        var db = _dbFactory.MarketDataDb;
        var futuresItiSignals = await db.Use(StoredProcedure.spGetFuturesItiSignalTrendClassData)
            .SetParameters(new { symbol, startDate, endDate })
            .ExecuteQueryAsync<FuturesItiSignalViewModel>();
        if ((futuresItiSignals?.Count ?? 0) > 0)
        {
            var queuedCommands = new List<object>();
            queuedCommands.Add(db.Use(StoredProcedure.spDeleteFuturesItiTrendClassData)
              .SetParameters(new { symbol })
              .QueueCommand());

            foreach (var e in futuresItiSignals)
            {
                queuedCommands.Add(
                db.Use(StoredProcedure.spInsertFuturesItiTrendClassData)
               .SetParameters(new
               {
                   symbol,
                   valueDate = e.ValueDate,
                   timestamp = e.IntrinsicTime,
                   trendClass = GetTrendClass(e, e.IntrinsicTimeMode == IntrinsicTimeModeType.TrendReversalChanged ? e.TrendReversal - e.TrendPrice : e.TrendExtreme- e.TrendPrice) ? 1 : 0,
                   //trendClass = GetTrendClass(e, e.IntrinsicTimeMode == IntrinsicTimeModeType.TrendDirectionChanged ? e.TrendExtreme - e.TrendPrice : e.TrendExtreme - e.IntrinsicPrice) ? 1 : 0,
                   trendDirection = e.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend ? 1 : 0,
                   trendDirectionMode = GetTrendDirectionMode(e.IntrinsicTimeMode),
                   trendDelta = e.IntrinsicTimeMode == IntrinsicTimeModeType.TrendReversalChanged ? e.TrendReversal - e.TrendPrice : e.TrendExtreme - e.TrendPrice,
                   //trendDelta = e.IntrinsicTimeMode == IntrinsicTimeModeType.TrendDirectionChanged ? e.TrendExtreme - e.TrendPrice : e.TrendExtreme - e.IntrinsicPrice,
                   futuresRSI = e.FuturesRSI == -1 ? 50 : e.FuturesRSI,
               })
               .QueueCommand());
            }
            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }
        if ((futuresItiSignals?.Count ?? 0) > 0)
        {
            var nd = Normal.Estimate(futuresItiSignals.Select(e => e.TrendDelta));
            trendDeltaDataStats = new FuturesItiTrendModelDataStatistics(
                Count: futuresItiSignals.Count,
                Maximum: futuresItiSignals.Max(e => e.TrendDelta),
                Mean: nd.Mean,
                Median: nd.Median,
                Minimum: futuresItiSignals.Min(e => e.TrendDelta),
                Skewness: nd.Skewness,
                StdDev: nd.StdDev,
                Variance: nd.Variance);
        }
        return trendDeltaDataStats;

        int GetTrendDirectionMode(IntrinsicTimeModeType e)
            => e switch
            {
                IntrinsicTimeModeType.TrendDirectionChanged => 0,
                IntrinsicTimeModeType.TrendExtremeChanged => 1,
                IntrinsicTimeModeType.TrendReversalChanged => -1,
                _ => 0
            };

        bool GetTrendClass(FuturesItiSignalViewModel e, double trendDelta)
            => e.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend
                ? trendDelta > e.TargetDelta / 2
                : trendDelta < -1 * (e.TargetDelta / 2);
    }

    /// <summary>
    /// insert futures rsi signal into storage
    /// </summary>
    /// <param name="e"></param>
    public async Task InsertFuturesRsiSignalAsync(FuturesRsiSignalReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertFuturesRsiSignal)
            .SetParameters(new {
                contractId = e.ContractId,
                valueDate = e.ValueDate,
                timestamp = e.Timestamp,
                signalType = $"{e.SignalType}",
                price = e.Price,
                priceChange = e.PriceChange,
                priceGain = e.PriceGain,
                priceLoss = e.PriceLoss,
                averagePriceGain = e.AveragePriceGain,
                averagePriceLoss = e.AveragePriceLoss,
                rs = e.RS,
                rsi = e.RSI,
                rsiAverage = e.RSIAverage,
                rsiSlope = e.RSISlope,
                windowSize = e.WindowSize })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert futures tdi signal
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFuturesTdiSignalAsync(FuturesTdiSignalReadModel e)
    => await _dbFactory.MarketDataDb
        .Use(StoredProcedure.spInsertFuturesTdiSignal)
            .SetParameters(new {
                contractId = e.ContractId,
                valueDate = e.ValueDate,
                timestamp = e.Timestamp,
                upTrendCount = e.UpTrendCount,
                downTrendCount = e.DownTrendCount,
                tdi = $"{e.TDI}",
                tdiStrength = $"{e.TDIStrength}" })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert futures tick data into storage
    /// </summary>
    /// <param name="tickData"></param>
    public async Task InsertFuturesTickDataAsync(FuturesTickDataViewModel e)
        =>  await _dbFactory.MarketDataDb
                .Use(StoredProcedure.spInsertFuturesTickData)
                .SetParameters(new {
                    contractId = e.ContractId,
                    valueDate = e.ValueDate,
                    tickDate = e.TickDate,
                    tickTime = e.TickTime,
                    price = e.Price,
                    size = e.Size })
                .ExecuteCommandAsync();

    /// <summary>
    /// insert collection of of futures tick data into storage
    /// </summary>
    /// <param name="tickData"></param>
    /// <returns></returns>
    public async Task InsertFuturesTickDataAsync(ICollection<FuturesTickDataViewModel> tickData)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.MarketDataDb;
        foreach (var e in tickData)
            queuedCommands.Add(db.Use(StoredProcedure.spInsertFuturesTickData)
                .SetParameters(new {
                    contractId = e.ContractId,
                    valueDate = e.ValueDate,
                    tickDate = e.TickDate,
                    tickTime = e.TickTime,
                    price = e.Price,
                    size = e.Size })
                .QueueCommand());
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// insert futures tick data into storage
    /// </summary>
    /// <param name="tickData"></param>
    public async Task InsertFuturesOptionTickDataAsync(FuturesOptionTickDataViewModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertFuturesOptionTickData)
            .SetParameters(new {
                contractId = e.ContractId,
                tickDate = e.TickDate,
                tickTime = e.TickTime,
                optionPrice = e.OptionPrice,
                bidPrice = e.BidPrice,
                askPrice = e.AskPrice,
                bidSize = e.BidSize,
                askSize = e.AskSize,
                impliedVolatility = e.ImpliedVolatility,
                delta = e.Delta,
                gamma = e.Gamma,
                vega = e.Vega,
                theta = e.Theta,
                rho = e.Rho,
                underlyingPrice = e.UnderlyingPrice })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert collection of futures tick data into storage
    /// </summary>
    /// <param name="tickData"></param>
    public async Task InsertFuturesOptionTickDataAsync(ICollection<FuturesOptionTickDataViewModel> optionTickData)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertFuturesOptionTickData)
            .SetParameters(optionTickData.Select(e => new {
                contractId = e.ContractId,
                tickDate = e.TickDate,
                tickTime = e.TickTime,
                optionPrice = e.OptionPrice,
                bidPrice = e.BidPrice,
                askPrice = e.AskPrice,
                bidSize = e.BidSize,
                askSize = e.AskSize,
                impliedVolatility = e.ImpliedVolatility,
                delta = e.Delta,
                gamma = e.Gamma,
                vega = e.Vega,
                theta = e.Theta,
                rho = e.Rho,
                underlyingPrice = e.UnderlyingPrice }))
            .ExecuteCommandAsync();

    /// <summary>
    /// insert futures option quote
    /// </summary>
    /// <param name="quotes"></param>
    /// <param name="quoteData"></param>
    /// <returns></returns>
    public async Task InsertFuturesOptionQuoteAsync(ICollection<FuturesOptionQuoteReadModel> quotes, ICollection<FuturesOptionQuoteDataReadModel> quoteData)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.MarketDataDb;
        foreach(var e in quotes)
            queuedCommands.Add(
            db.Use(StoredProcedure.spInsertFuturesOptionQuote)
                .SetParameters( new
                {
                    contractId = e.ContractId,
                    requestId = e.RequestId,
                    quoteId = e.QuoteId,
                    createdBy = e.CreatedBy,
                    createdOn = e.CreatedOn,
                }).QueueCommand());

        foreach (var e in quoteData)
            queuedCommands.Add(db.Use(StoredProcedure.spInsertFuturesOptionQuoteData)
                .SetParameters(new
                {
                    contractId = e.ContractId,
                    requestId = e.RequestId,
                    bidPrice = e.BidPrice,
                    bidSize = e.BidSize,
                    askPrice = e.AskPrice,
                    askSize = e.AskSize,
                }).QueueCommand());

        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// insert futures option quote data
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task  InsertFuturesOptionQuoteDataAsync(FuturesOptionQuoteDataReadModel e)
        => await  _dbFactory.MarketDataDb
                .Use(StoredProcedure.spInsertFuturesOptionQuoteData)
                .SetParameters(new
                {
                    contractId = e.ContractId,
                    requestId = e.RequestId,
                    bidPrice = e.BidPrice,
                    bidSize = e.BidSize,
                    askPrice = e.AskPrice,
                    askSize = e.AskSize,
                }).ExecuteCommandAsync();
    
    /// <summary>
    /// insert streaming data log info
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="errorCode"></param>
    /// <param name="errorMessage"></param>
    public async Task InsertStreamingDataLogAsync(DateTime valueDate, int errorCode, string errorMessage)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertStreamingDataLog)
            .SetParameters(new {
                valueDate,
                errorCode,
                errorMessage = errorMessage ?? string.Empty })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert trade live feed
    /// </summary>
    /// <param name="tradeLiveFeed"></param>
    public async Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertTradeLiveFeed)
            .SetParameters(new {
                orderId = e.OrderId,
                tradeId = e.TradeId,
                liveFeed = e.LiveFeed })
            .ExecuteCommandAsync();

    /// <summary>
    /// return number of trading days...
    /// </summary>
    /// <param name="equityMarketType"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<int> GetTradingDaysAsync( 
        DateTime startDate, 
        DateTime endDate, 
        MarketType marketType = MarketType.Futures, 
        CurrencyType currencyType = CurrencyType.USD)
    {
        var key = new TradingDaysKey(
            StartDate: startDate,
            EndDate: endDate,
            MarketType: marketType,
            CurrencyType: currencyType );

        if (_tradingDaysMap.ContainsKey(key))
            return _tradingDaysMap[key];

        // load market holidays by currency type..
        var marketHolidays = await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetMarketHolidays)
            .SetParameters(new { currencyType = $"{currencyType}" })
            .ExecuteQueryAsync<MarketHolidayReadModel>();

        // build holiday map...
        var holidayMap = new Dictionary<DateTime, MarketHolidayReadModel>();
        foreach(var e in marketHolidays)
            holidayMap.Add(e.HolidayDate.ToDateTime(TimeOnly.MinValue), e);

        // calculate trading days based on total number of days from start date to end date
        // that do not fall on a weekend or holiday...
        var dateIndex = 0;
        var tradingDays = 0;
        while(startDate.AddDays(dateIndex) <= endDate)
        {
            var tradeDate = startDate.AddDays(dateIndex++);
            if (tradeDate.DayOfWeek == DayOfWeek.Saturday
                || tradeDate.DayOfWeek == DayOfWeek.Sunday
                || holidayMap.Keys.Contains(tradeDate) )
                continue;
            tradingDays++;
        }
        _tradingDaysMap.Add(key, tradingDays);
        return tradingDays;
   }

    public async Task<DateTime[]> GetTradingDatesAsync(
       DateTime startDate,
       DateTime endDate,
       MarketType marketType = MarketType.Futures,
       CurrencyType currencyType = CurrencyType.USD)
    {

        // load market holidays by currency type..
        var marketHolidays = await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spGetMarketHolidays)
            .SetParameters(new { currencyType = $"{currencyType}" })
            .ExecuteQueryAsync<MarketHolidayReadModel>();

        // build holiday map...
        var holidayMap = new Dictionary<DateTime, MarketHolidayReadModel>();
        foreach (var e in marketHolidays)
            holidayMap.Add(e.HolidayDate.ToDateTime(TimeOnly.MinValue), e);

        // calculate trading days based on total number of days from start date to end date
        // that do not fall on a weekend or holiday...
        var dateIndex = 0;
        var tradingDates = new List<DateTime>();
        while (startDate.AddDays(dateIndex) <= endDate)
        {
            var tradeDate = startDate.AddDays(dateIndex++);
            if (tradeDate.DayOfWeek == DayOfWeek.Saturday
                || tradeDate.DayOfWeek == DayOfWeek.Sunday
                || holidayMap.Keys.Contains(tradeDate))
                continue;
            tradingDates.Add(tradeDate);
        }
        return tradingDates.ToArray();
    }

    /// <summary>
    /// insert futures bar data
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFuturesBarDataAsync(FuturesBarDataReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertFuturesBarData)
            .SetParameters(new {
                contractId = e.ContractId,
                symbol = e.Symbol,
                valueDate = e.ValueDate,
                barDate = e.BarDate,
                barRateType = $"{e.BarRateType}",
                barValue = e.BarValue,
                upTrendTrigger = e.UpTrendTrigger,
                downTrendTrigger = e.DownTrendTrigger })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert futures closing price
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFuturesClosingPriceAsync(FuturesClosingPriceReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertFuturesClosingPrice)
            .SetParameters(new {
                contractId = e.ContractId,
                valueDate = e.ValueDate,
                closingPrice = e.ClosingPrice,
                createdOn = e.CreatedOn,
                createdBy = e.CreatedBy })
        .ExecuteCommandAsync();

    /// <summary>
    /// insert futures eod data
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFuturesEodDataAsync(FuturesEodDataViewModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertFuturesEodData)
            .SetParameters(new {
                contractId = e.ContractId,
                valueDate = e.ValueDate,
                openPrice = e.OpenPrice,
                highPrice = e.HighPrice,
                lowPrice = e.LowPrice,
                closePrice = e.ClosePrice,
                volume = e.Volume,
                dailyPercentChange = e.DailyPercentChange,
                dailyStdDev = e.DailyStdDev,
                dailyStdDevAmount = e.DailyStdDevAmount,
                upperBand = e.UpperBand,
                mean = e.Mean,
                lowerBand = e.LowerBand,
                marketDirection = $"{e.MarketDirection}",
                marketVolatility = $"{e.MarketVolatility}",
                priceDirection = $"{e.PriceDirection}",
                priceVolatility = $"{e.PriceVolatility}",
                marketDirectionIndicator = e.MarketDirectionIndicator,
                windowSize = e.WindowSize
            })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert vix futures eod data
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertVixFuturesEodDataAsync(FuturesTickDataViewModel e)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.MarketDataDb;
        queuedCommands.Add(db.Use(StoredProcedure.spInsertVixFuturesEodData)
            .SetParameters(new {
                contractId = e.ContractId,
                valueDate = e.ValueDate,
                price = e.Price,
                size = e.Size })
            .QueueCommand());
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    public async Task UpdateFuturesEodDataNearestStrikesAsync(
        string contractId, 
        DateTime valueDate, 
        int nearestPutStrike, 
        int nearestCallStrike)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spUpdateFuturesEodDataNearestStrikes)
            .SetParameters(new {
                contractId,
                valueDate,
                nearestPutStrike,
                nearestCallStrike })
            .ExecuteCommandAsync();

    public async Task UpdateFuturesContractAsync(string originalContractId, FuturesContractViewModel e)
        => await _dbFactory.MarketDataDb
                .Use(StoredProcedure.spUpdateFuturesContract)
                .SetParameters(new {
                    originalContractId,
                    contractId = e.ContractId,
                    description = e.Description,
                    symbol = e.Symbol,
                    localSymbol = e.LocalSymbol,
                    securityType = e.SecurityType,
                    currency = e.Currency,
                    exchange = e.Exchange,
                    multiplier = e.Multiplier,
                    lastTradeDate = e.LastTradeDate,
                    currentlyTraded = e.CurrentlyTraded })
                .ExecuteCommandAsync();

    /// <summary>
    /// update futures eod data
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    /// <param name="openPrice"></param>
    /// <param name="highPrice"></param>
    /// <param name="lowPrice"></param>
    /// <param name="closePrice"></param>
    /// <param name="volume"></param>
    /// <returns></returns>
    public async Task UpdateFuturesEodDataAsync(
        string symbol, 
        DateTime valueDate, 
        double openPrice, 
        double highPrice, 
        double lowPrice, 
        double closePrice, 
        int volume)
        => await _dbFactory.MarketDataDb
                .Use(StoredProcedure.spUpdateFuturesEodData)
                .SetParameters(new {
                    symbol,
                    valueDate,
                    openPrice,
                    highPrice,
                    lowPrice,
                    closePrice,
                    volume })
                .ExecuteCommandAsync();

    /// <summary>
    /// update futures option contract
    /// </summary>
    /// <param name="originalContractId"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task UpdateFuturesOptionContractAsync(string originalContractId, FuturesOptionContractReadModel e)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.MarketDataDb;

        // delete original contract...
        queuedCommands.Add(
            db.Use(StoredProcedure.spDeleteFuturesOptionContract)
                .SetParameters(new { contractId = originalContractId })
                .QueueCommand());

        // insert updated contract...
        queuedCommands.Add(db.Use(StoredProcedure.spInsertFuturesOptionContract)
            .SetParameters(new {
                contractId = e.ContractId,
                description = e.Description ?? string.Empty,
                symbol = e.Symbol,
                localSymbol = e.LocalSymbol,
                securityType = e.SecurityType,
                currency = e.Currency,
                exchange = e.Exchange,
                multiplier = e.Multiplier,
                contractMonth = e.ContractMonth,
                strikePrice = e.StrikePrice,
                optionType = e.OptionType })
            .QueueCommand());

        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// insert yield curve rate
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertYieldCurveRateAsync(YieldCurveRateReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertYieldCurveRate)
            .SetParameters(new {
                valueDate = e.ValueDate,
                oneMonth = e.OneMonth,
                twoMonth = e.TwoMonth,
                threeMonth = e.ThreeMonth,
                sixMonth = e.SixMonth,
                oneYear = e.OneYear,
                twoYear = e.TwoYear,
                threeYear = e.ThreeYear,
                fiveYear = e.FiveYear,
                sevenYear = e.SevenYear,
                tenYear = e.TenYear,
                twentyYear = e.TwentyYear,
                thirtyYear = e.ThirtyYear })
            .ExecuteCommandAsync();

    /// <summary>
    /// insert collection of yieldCurveRates into storage
    /// </summary>
    /// <param name="yieldCurveRates"></param>
    /// <returns></returns>
    public async Task InsertYieldCurveRatesAsync(ICollection<YieldCurveRateReadModel> yieldCurveRates)
    {
        var queuedCommands = new List<object>();    
        var db = _dbFactory.MarketDataDb;
        foreach (var e in yieldCurveRates)
            queuedCommands.Add(db.Use(StoredProcedure.spInsertYieldCurveRate)
                .SetParameters(new  {
                    valueDate = e.ValueDate,
                    oneMonth = e.OneMonth,
                    twoMonth = e.TwoMonth,
                    threeMonth = e.ThreeMonth,
                    sixMonth = e.SixMonth,
                    oneYear = e.OneYear,
                    twoYear = e.TwoYear,
                    threeYear = e.ThreeYear,
                    fiveYear = e.FiveYear,
                    sevenYear = e.SevenYear,
                    tenYear = e.TenYear,
                    twentyYear = e.TwentyYear,
                    thirtyYear = e.ThirtyYear })
                .QueueCommand());
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// insert rate of return
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertRateOfReturnAsync(RateOfReturnReadModel e)
        => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spInsertRateOfReturn)
            .SetParameters(new {
                symbol = e.Symbol,
                valueDate = e.ValueDate,
                rateOfReturn = e.RateOfReturn })
            .ExecuteCommandAsync();

    /// <summary>
    /// update yield curve rate
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task UpdateYieldCurveRateAsync(YieldCurveRateReadModel e)
         => await _dbFactory.MarketDataDb
            .Use(StoredProcedure.spUpdateYieldCurveRate)
            .SetParameters(new {
                valueDate = e.ValueDate,
                oneMonth = e.OneMonth,
                twoMonth = e.TwoMonth,
                threeMonth = e.ThreeMonth,
                sixMonth = e.SixMonth,
                oneYear = e.OneYear,
                twoYear = e.TwoYear,
                threeYear = e.ThreeYear,
                fiveYear = e.FiveYear,
                sevenYear = e.SevenYear,
                tenYear = e.TenYear,
                twentyYear = e.TwentyYear,
                thirtyYear = e.ThirtyYear })
            .ExecuteCommandAsync();

    /// <summary>
    /// check if futures contract exists for selected contract id
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task<bool> GetFuturesContractExistsAsync(string contractId)
       => await _dbFactory.MarketDataDb
           .Use(StoredProcedure.spGetFuturesContractExists)
           .SetParameters(new { contractId })
           .ExecuteScalarAsync<bool>();

    /// <summary>
    /// check if futures option contract exists for selected contract id
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task<bool> GetFuturesOptionContractExistsAsync(string contractId)
       => await _dbFactory.MarketDataDb
           .Use(StoredProcedure.spGetFuturesOptionContractExists)
           .SetParameters(new { contractId })
           .ExecuteScalarAsync<bool>();

    /// <summary>
    /// check if yield curve rate exists for selected value date
    /// </summary>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<bool> GetYieldCurveRateExistsAsync(DateTime valueDate)
       => await _dbFactory.MarketDataDb
           .Use(StoredProcedure.spGetYieldCurveRateExists)
           .SetParameters(new { valueDate })
           .ExecuteSingleAsync<bool>(e => e.Get("YieldCurveRateExists").As<bool>());

    /// <summary>
    /// backup market data database
    /// </summary>
    /// <param name="backupType"></param>
    /// <param name="commandTimeout"></param>
    /// <param name="onInfoMessage"></param>
    /// <returns></returns>
    public async Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage)
    {
            var db = _dbFactory.MarketDataDb;
            await db.Use(StoredProcedure.spBackupDatabase)
                .SetParameters(new { backupType = $"{backupType}" })
                .WithNoTransaction()
                .SetCommandTimeout(commandTimeout)
                .ExecuteCommandAsync(onInfoMessage);
    }

 
    private record TradingDaysKey(
        DateTime StartDate,
        DateTime EndDate,
        MarketType MarketType,
        CurrencyType CurrencyType)
    {
        public override string ToString() => $"{StartDate:yyyy-MM-dd}|{EndDate:yyyy-MM-dd}|{MarketType}|{CurrencyType}";
    }
}
