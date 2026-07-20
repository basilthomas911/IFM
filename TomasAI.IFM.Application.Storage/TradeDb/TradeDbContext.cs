using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Framework.Storage;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Application.Storage.TradeDb
{
    /// <summary>
    /// trade database
    /// </summary>
    public class TradeDbContext : ObjectDataRepository<TradeDbContext>, ITradeDbContext, ITradeDbReadContext, ITradeDbWriteContext
    {
        public const string TradeDbConnection = "TradeDbConnection";
        IDbContextFactory _dbFactory;

        /// <summary>
        /// trade database constructor
        /// </summary>
        /// <param name="connectionSettings"></param>
        public TradeDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<TradeDbContext> logger)
            : base(connectionSettings[TradeDbConnection], logger)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        /// <summary>
        /// initialize trade view model mappings
        /// </summary>
        /// <param name="model"></param>
        public override void OnCreateModel(DbModel<TradeDbContext> model)
        {
            OptionTrade = model.Map(e => e.OptionTrade)
              .Parameters(e =>
                  e.Set(o => o.OrderId)
                   .Set(o => o.TradeId)
                   .Set(o => o.TradeStrategy)
                   .Set(o => o.TradeDate)
                   .Set(o => o.MaturityDate)
                   .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                   .Set(o => o.TradeState, o => o.AsEnum<TradeState>())
                   .Set(o => o.TradeAction, o => o.AsEnum<TradeAction>())
                   .Set(o => o.UnderlyingContractId)
                   .Set(o => o.UnderlyingAssetType, o => o.AsEnum<AssetType>())
                   .Set(o => o.IsPrimaryTrade)
                   .Set(o => o.IsHedgeTrade)
                   .Set(o => o.CreatedOn)
                   .Set(o => o.CreatedBy)
                   .Set(o => o.UpdatedOn)
                   .Set(o => o.UpdatedBy)
              );

            TradePosition = model.Map(e => e.TradePosition)
                .Parameters(e =>
                    e.Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     .Set(o => o.ValueDate)
                     .Set(o => o.DaysToExpiry)
                     .Set(o => o.TradeStatus, o => o.AsEnum<TradeStatus>())
                     .Set(o => o.Commission)
                     .Set(o => o.DeltaHedge)
                     .Set(o => o.NetSpread)
                     .Set(o => o.TradeValue)
                     .Set(o => o.TradePnl)
                     .Set(o => o.AssetPrice)
                     .Set(o => o.OTMProbability)
                     .Set(o => o.ForwardPrice)
                     .Set(o => o.ForwardLossRatio)
                     .Set(o => o.LossProbability)
                     .Set(o => o.RiskFreeRate)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                     .Set(o => o.UpdatedOn)
                     .Set(o => o.UpdatedBy)
                );

            OptionLeg = model.Map(e => e.OptionLeg)
                .Parameters(e =>
                    e.Set(o => o.TradeId)
                     .Set(o => o.ContractId)
                     .Set(o => o.Quantity)
                     .Set(o => o.StrikePrice)
                     .Set(o => o.OptionLegType, o => o.AsEnum<OptionType>())
                     .Set(o => o.OptionLegAction, o => o.AsEnum<OptionLegAction>())
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                     .Set(o => o.UpdatedOn)
                     .Set(o => o.UpdatedBy)
                );

            OptionLegData = model.Map(e => e.OptionLegData)
                .Parameters(e =>
                    e.Set(o => o.TradeId)
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     .Set(o => o.ValueDate)
                     .Set(o => o.DaysToExpiry)
                     .Set(o => o.TradeStatus, o => o.AsEnum<TradeStatus>())
                     .Set(o => o.OptionLegId)
                     .Set(o => o.BidPrice)
                     .Set(o => o.AskPrice)
                     .Set(o => o.ImpliedVolatility)
                     .Set(o => o.Delta)
                     .Set(o => o.Gamma)
                     .Set(o => o.Theta)
                     .Set(o => o.Vega)
                     .Set(o => o.Rho)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                     .Set(o => o.UpdatedOn)
                     .Set(o => o.UpdatedBy)
                );

            OptionTradeSpreadData = model.Map(e => e.OptionTradeSpreadData)
                .Parameters(e =>
                    e.Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     .Set(o => o.ValueDate)
                     .Set(o => o.LossLimit)
                     .Set(o => o.WinLimit)
                     .Set(o => o.ForwardSpread)
                     .Set(o => o.NetSpread)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                );

            OptionTradeSpreadBarData = model.Map(e => e.OptionTradeSpreadBarData)
                .Parameters(e =>
                    e.Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     .Set(o => o.ValueDate)
                     .Set(o => o.BarDate)
                     .Set(o => o.LossLimit)
                     .Set(o => o.WinLimit)
                     .Set(o => o.ForwardSpread)
                     .Set(o => o.NetSpread)
                );

            TradeHistory = Map(e => e.TradeHistory)
              .Parameters(e =>
                  e.Set(o => o.OrderId)
                   .Set(o => o.TradeId)
                   .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                   .Set(o => o.ValueDate)
                   .Set(o => o.DaysToExpiry)
                   .Set(o => o.TradeStatus, o => o.AsEnum<TradeStatus>())
                   .Set(o => o.Commission)
                   .Set(o => o.NetSpread)
                   .Set(o => o.TradePnl)
              );

            TradeLimit = Map(e => e.TradeLimit)
               .Parameters(e =>
                   e.Set(o => o.TradeId)
                    .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                    .Set(o => o.RiskMargin)
                    .Set(o => o.MaxProfit)
                    .Set(o => o.MaxLoss)
                    .Set(o => o.MaxReturn)
                    .Set(o => o.MaxLossLimit)
                    .Set(o => o.MinProfitLimit)
                    .Set(o => o.MaxProfitLimit)
                    .Set(o => o.MinProfitTarget)
                    .Set(o => o.DailyProfitTarget)
                    .Set(o => o.CreatedOn)
                    .Set(o => o.CreatedBy)
                    .Set(o => o.UpdatedOn)
                    .Set(o => o.UpdatedBy)
               );

            TradeTypeLimit = Map(e => e.TradeTypeLimit)
               .Parameters(e =>
                   e.Set(o => o.TradeId)
                    .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                    .Set(o => o.MaxLossLimit)
                    .Set(o => o.MinProfitLimit)
                    .Set(o => o.MaxProfitLimit)
               );

        
            TradePlan = Map(e => e.TradePlan)
                .Parameters(e =>
                    e.Set(o => o.TradePlanId, o => o.AsGuid())
                     .Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     .Set(o => o.TradeDate)
                     .Set(o => o.ValueDate)
                     .Set(o => o.MaturityDate)
                     .Set(o => o.ActionDate)
                     .Set(o => o.ActionType, o => o.AsEnum<ActionType>())
                     .Set(o => o.ActionSubType, o => o.AsEnum<ActionSubType>())
                     .Set(o => o.ActionState, o => o.AsEnum<ActionState>())
                     .Set(o => o.ActionReason)
                     .Set(o => o.TradePnl)
                     .Set(o => o.ForwardLossRatio)
                     .Set(o => o.LossProbability)
                     .Set(o => o.MScore)
                     .Set(o => o.MaxProfit)
                     .Set(o => o.MaxLoss)
                     .Set(o => o.MinProfitTarget)
                     .Set(o => o.DailyProfitTarget)
                     .Set(o => o.AssetPrice)
                     .Set(o => o.AssetStdDev)
                     .Set(o => o.AssetMean)
                     .Set(o => o.AssetPriceChange)
                     .Set(o => o.MarketTrend, o => o.AsEnum<MarketDirectionType>())
                     .Set(o => o.MarketVolatility, o => o.AsEnum<MarketVolatilityType>())
                     .Set(o => o.MarketDirection, o => o.AsEnum<PriceDirectionType>())
                     .Set(o => o.VixVolatility, o => o.AsEnum<PriceVolatilityType>())
                     .Set(o => o.TradeRisk, o => o.AsEnum<TradeRiskType>())
                     .Set(o => o.FiftyDayMA)
                     .Set(o => o.FiveDayXMA)
                     .Set(o => o.PutOTMProbability)
                     .Set(o => o.CallOTMProbability)
                     .Set(o => o.ShortPutGamma)
                     .Set(o => o.ShortCallGamma)
                     .Set(o => o.GammaRisk, o => o.AsEnum<GammaRiskType>())
                     .Set(o => o.NetPrice)
                     .Set(o => o.ForwardPrice)
                     .Set(o => o.ForwardDelta)
                     .Set(o => o.StopLossLimit)
                     .Set(o => o.TrendType, o => o.AsEnum<FuturesTrendType>())
                     .Set(o => o.TrendStrength, o => o.AsEnum<FuturesTrendStrengthType>())
                     .Set(o => o.RSI)
                     .Set(o => o.RSISlope)
                     .Set(o => o.TDI, o => o.AsEnum<FuturesTrendDirectionType>())
                     .Set(o => o.TDIStrength, o => o.AsEnum<FuturesTrendDirectionStrengthType>())
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                );

            TradeOrder = Map(e => e.TradeOrder)
                .Parameters(e =>
                    e.Set(o => o.FundId)
                     .Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.ValueDate)
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     .Set(o => o.TradeSubType, o => o.AsEnum<TradeSubType>())
                     .Set(o => o.TradeDate)
                     .Set(o => o.MaturityDate)
                     .Set(o => o.TradeOrderState, o => o.AsEnum<TradeOrderState>())
                     .Set(o => o.UnderlyingContractId)
                     .Set(o => o.UnderlyingAssetType, o => o.AsEnum<AssetType>())
                     .Set(o => o.OrderDescription)
                     .Set(o => o.OrderAction, o => o.AsEnum<OrderAction>())
                     .Set(o => o.OrderActionType, o => o.AsEnum<OrderActionType>())
                     .Set(o => o.OrderQuantity)
                     .Set(o => o.OrderType, o => o.AsEnum<OrderType>())
                     .Set(o => o.OrderPrice)
                     .Set(o => o.OrderAmount)
                     .Set(o => o.Commission)
                     .Set(o => o.TotalAmount)
                     .Set(o => o.TradePnl)
                     .Set(o => o.TradeFillType, o => o.AsEnum<TradeFillType>())
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                     .Set(o => o.UpdatedOn)
                     .Set(o => o.UpdatedBy)
                 );

            TradeFill = Map(e => e.TradeFill)
               .Parameters(e =>
                   e.Set(o => o.FundId)
                    .Set(o => o.OrderId)
                    .Set(o => o.TradeId)
                    .Set(o => o.FillDate)
                    .Set(o => o.FillQuantity)
                    .Set(o => o.CreatedOn)
                    .Set(o => o.CreatedBy)
               );

            TradeFillData = Map(e => e.TradeFillData)
                .Parameters(e =>
                     e.Set(o => o.FundId)
                      .Set(o => o.OrderId)
                      .Set(o => o.TradeId)
                      .Set(o => o.ContractId)
                      .Set(o => o.FillDate)
                      .Set(o => o.BidPrice)
                      .Set(o => o.AskPrice)
                      .Set(o => o.Commission)
                      .Set(o => o.OptionLegAction, o => o.AsEnum<OptionLegAction>())
                      .Set(o => o.CreatedOn)
                      .Set(o => o.CreatedBy)
                );

            TradeLiveFeed = Map(e => e.TradeLiveFeed)
                .Parameters(e =>
                    e.Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.LiveFeed)
                );

            TradePlanForwardLossRatio = Map(e => e.TradePlanForwardLossRatio)
                .Parameters(e =>
                    e.Set(o => o.ForwardLossRatio)
                );

            TradePlanStopLossLimit = Map(e => e.TradePlanStopLossLimit)
                .Parameters(e =>
                    e.Set(o => o.StopLossLimit)
                );

            TradeDiary = Map(e => e.TradeDiary)
                .Parameters(e =>
                    e.Set(o => o.EntryDate)
                     .Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.ValueDate)
                     .Set(o => o.TradeStatus, o => o.AsEnum<TradeStatus>())
                     .Set(o => o.ActionSource, o => o.AsEnum<ActionSource>())
                     .Set(o => o.ActionType, o => o.AsEnum<ActionType>())
                     .Set(o => o.ActionSubType, o => o.AsEnum<ActionSubType>())
                     .Set(o => o.ActionState, o => o.AsEnum<ActionState>())
                     .Set(o => o.ActionReason)
                     .Set(o => o.ActionDataType)
                     .Set(o => o.ActionData)
                );

            TradePlanAction = Map(e => e.TradePlanAction)
                .Parameters(e =>
                    e.Set(o => o.TradePlanId, o => o.AsGuid())
                     .Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.ValueDate)
                     .Set(o => o.ActionDate)
                     .Set(o => o.ActionType, o => o.AsEnum<ActionType>())
                     .Set(o => o.ActionSubType, o => o.AsEnum<ActionSubType>())
                     .Set(o => o.ActionState, o => o.AsEnum<ActionState>())
                     .Set(o => o.ActionReason)
                     .Set(o => o.MarketTrend, o => o.AsEnum<MarketDirectionType>())
                     .Set(o => o.MarketVolatility, o => o.AsEnum<MarketVolatilityType>())
                     .Set(o => o.MarketDirection, o => o.AsEnum<PriceDirectionType>())
                     .Set(o => o.VixVolatility, o => o.AsEnum<PriceVolatilityType>())
                     .Set(o => o.TradeRisk, o => o.AsEnum<TradeRiskType>())
                     .Set(o => o.GammaRisk, o => o.AsEnum<GammaRiskType>())
                     .Set(o => o.TradePnl)
                     .Set(o => o.ForwardLossRatio)
                     .Set(o => o.MScore)
                     .Set(o => o.NetPrice)
                     .Set(o => o.ForwardPrice)
                     .Set(o => o.StopLossLimit)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                );

            TradePositionState = Map(e => e.TradePositionState)
                .Parameters(e =>
                    e.Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.TradePositionState, o => o.AsEnum<TradePositionState>())
                     .Set(o => o.OpenedOn)
                     .Set(o => o.OpenedBy)
                );

            TradePlanForwardLossLimit = Map(e => e.TradePlanForwardLossLimit)
                .Parameters(e =>
                    e.Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     .Set(o => o.ValueDate)
                     .Set(o => o.LimitType, o => o.AsEnum<ForwardLossLimitType>())
                );

            TradePlacementSignal = Map(e => e.TradePlacementSignal)
                .Parameters(e =>
                    e.Set(o => o.ContractId)
                     .Set(o => o.ValueDate)
                     .Set(o => o.TradePlacementSignal, o => o.AsEnum<TradePlacementSignalType>())
                     .Set(o => o.TradePrice)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                );

        }

        /// <summary>
        /// return db reader/writer properties
        /// </summary>
        public ITradeDbReadContext DbReader => this;
        public ITradeDbWriteContext DbWriter => this;
        
        public DbMap<OptionTradeViewModel> OptionTrade { get; private set; }
        public DbMap<TradePositionReadModel> TradePosition { get; private set; }
        public DbMap<OptionLegReadModel> OptionLeg { get; private set; }
        public DbMap<OptionLegDataReadModel> OptionLegData { get; private set; }
        public DbMap<OptionTradeSpreadDataViewModel> OptionTradeSpreadData { get; private set; }
        public DbMap<OptionTradeSpreadBarDataViewModel> OptionTradeSpreadBarData { get; private set; }
        public DbMap<TradeHistoryReadModel> TradeHistory { get; private set; }
        public DbMap<TradeLimitReadModel> TradeLimit { get; private set; }
        public DbMap<TradeTypeLimitReadModel> TradeTypeLimit { get; private set; }
        public DbMap<TradeOrderReadModel> TradeOrder { get; private set; }
        public DbMap<TradeFillReadModel> TradeFill { get; private set; }
        public DbMap<TradeFillDataReadModel> TradeFillData { get; private set; }
        public DbMap<TradePlanReadModel> TradePlan { get; private set; }
        public DbMap<TradePlanActionReadModel> TradePlanAction { get; private set; }
        public DbMap<TradeLiveFeedReadModel> TradeLiveFeed { get; private set; }
        public DbMap<TradePlanForwardLossRatioReadModel> TradePlanForwardLossRatio { get; private set; }
        public DbMap<TradePlanStopLossLimitReadModel> TradePlanStopLossLimit { get; private set; }
        public DbMap<TradeDiaryEntryReadModel> TradeDiary { get; private set; }
        public DbMap<TradePositionStateReadModel> TradePositionState { get; private set; }
        public DbMap<TradePlanForwardLossLimitReadModel> TradePlanForwardLossLimit { get; private set; }
        public DbMap<TradePlacementSignalReadModel> TradePlacementSignal { get; private set; }

        internal enum StoredProcedure
        {
            spBackupDatabase,
            spDeleteOptionTrade,
            spDeleteOptionTradeSpreadBarData,
            spDeleteTradeTypeLimit,
            spDeleteTradeLiveFeed,
            spDeleteTradeLiveFeeds,
            spDeleteTradePlanForwardLossLimit,
            spDeleteTradePositionState,
            spGetIronCondorTradePrice,
            spGetOptionLegs,
            spGetOptionLegData,
            spGetOptionTrade,
            spGetOptionTrades,
            spGetOptionTradeSpreadData,
            spGetOptionTradeSpreadBarData,
            spGetTradePosition,
            spGetTradePositions,
            spGetTradePositionTradeTypes,
            spGetTradeHistory,
            spGetTradeLimit,
            spGetTradeOrder,
            spGetTradeOrderByValueDate,
            spGetTradeTypeLimit,
            spGetTradeTypeLimits,
            spGetTradeFills,
            spGetTradeFillData,
            spGetTradePlan,
            spGetTradePlans,
            spGetTradePlanByDateRange,
            spGetTradePlanAction,
            spGetTradePlanForwardLossRatio,
            spGetTradePlanForwardLossLimit,
            spGetLastTradePlanForwardLossRatio,
            spGetTradePlanStopLossLimit,
            spGetTradePositionState,
            spGetTradeOrders,
            spGetTradeLiveFeed,
            spGetTradeDiary,
            spInsertOptionTradeSpreadBarData,
            spInsertOptionTradeSpreadData,
            spInsertOptionLeg,
            spInsertOptionLegData,
            spInsertOptionTrade,
            spInsertTradePosition,
            spInsertOrder,
            spInsertTradeLimit,
            spInsertTradeTypeLimit,
            spInsertTradeFill,
            spInsertTradeFillData,
            spInsertTradePlacementSignal,
            spInsertTradePlan,
            spInsertTradePlanAction,
            spInsertTradePlanForwardLossLimit,
            spInsertTradeLiveFeed,
            spInsertTradeOrder,
            spInsertTradeDiary,
            spInsertTradePositionState,
            spUpdateOptionLegData,
            spUpdateTradePosition,
            spUpdateOptionTradeState,
            spUpdateTradePositionStatus,
            spUpdateTradeLimitDailyProfitTarget,
            spUpdateTradeLiveFeed,
            spUpdateTradeOrderState,
            spUpdateTradeOrderOrderPrice
        }

        public class FieldNames
        {
            public readonly string TradePositionState;
        }

        /// <summary>
        /// return collection of option trades
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<OptionTradeViewModel>> GetOptionTradesAsync(int orderId)
        {
            var optionTrades = new List<OptionTradeViewModel>();
            var db = _dbFactory.TradeDb;
            foreach (var e in await db.Use(StoredProcedure.spGetOptionTrades)
                    .SetParameters(new { orderId })
                    .ExecuteQueryAsync<OptionTradeViewModel>())
                optionTrades.Add(await FillOptionTradeAsync(e));
            return optionTrades;
        }

        /// <summary>
        /// return option trade by trade id
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<OptionTradeViewModel> GetOptionTradeAsync(int orderId, int tradeId)
        {
            var db = _dbFactory.TradeDb;
            var optionTrade = await db.Use(StoredProcedure.spGetOptionTrade)
                   .SetParameters(new { orderId, tradeId })
                   .ExecuteSingleAsync<OptionTradeViewModel>();
            if (optionTrade is not null)
                optionTrade = await FillOptionTradeAsync(optionTrade);
            return optionTrade;
        }

        /// <summary>
        /// return option trade spread data
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="tradeType"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<OptionTradeSpreadDataViewModel> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, TradeType tradeType, DateTime valueDate)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetOptionTradeSpreadData)
                .SetParameters(new {
                    orderId,
                    tradeId,
                    tradeType = $"{tradeType}",
                    valueDate })
                .ExecuteSingleAsync<OptionTradeSpreadDataViewModel>();
        }

        /// <summary>
        /// return option trade spread bar data
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="tradeType"></param>
        /// <param name="valueDate"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<OptionTradeSpreadBarDataViewModel>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, TradeType tradeType, DateTime valueDate, DateTime startDate, DateTime endDate)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetOptionTradeSpreadBarData)
                .SetParameters(new {
                    orderId,
                    tradeId,
                    tradeType = $"{tradeType}",
                    valueDate,
                    startDate,
                    endDate })
                .ExecuteQueryAsync<OptionTradeSpreadBarDataViewModel>();
        }

        /// <summary>
        /// return iron condor tradee price
        /// </summary>
        /// <param name="tradeId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<TradePriceReadModel> GetIronCondorTradePriceAsync(int tradeId, DateTime valueDate)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetOptionLegs)
                .SetParameters(new { tradeId, valueDate = valueDate.Date })
                .ExecuteSingleAsync<TradePriceReadModel>();
        }

        /// <summary>
        /// fill option trade with option trade graph data
        /// </summary>
        /// <param name="optionTrade"></param>
        /// <returns></returns>
        async Task<OptionTradeViewModel> FillOptionTradeAsync(OptionTradeViewModel optionTrade)
        {
            var db = _dbFactory.TradeDb;
            var tradePositions = await db
                .Use(StoredProcedure.spGetTradePositions)
                .SetParameters(new { orderId = optionTrade.OrderId, tradeId = optionTrade.TradeId })
                .ExecuteQueryAsync<TradePositionReadModel>();

            var optionLegs = (await db
                .Use(StoredProcedure.spGetOptionLegs)
                .SetParameters(new { tradeId = optionTrade.TradeId })
                .ExecuteQueryAsync<OptionLegReadModel>()).ToList();

            var tradePosition = tradePositions.Select(o =>
                o.AddOptionLegData(GetOptionLegData(o.Key.TradeId, o.Key.TradeType, o.Key.ValueDate, o.Key.DaysToExpiry, o.Key.TradeStatus)
                    .Result.Select(e => e.SetOptionLeg(optionLegs.Where(ol => ol.ContractId == e.OptionLegId).Single())).ToList())).ToList();
 
            var tradeLimit = await GetTradeLimitAsync(optionTrade.TradeId);

            var tradeTypeLimits = (await db
                    .Use(StoredProcedure.spGetTradeTypeLimits)
                    .SetParameters(new { tradeId = optionTrade.TradeId })
                    .ExecuteQueryAsync<TradeTypeLimitReadModel>()).ToList();

            var tradeFills = (await GetTradeFillsAsync(optionTrade.TradeId)).ToList();

            return optionTrade
                .AddOptionLegs(optionLegs)
                .AddTradePosition(tradePosition)
                .SetTradeLimit(tradeLimit)
                .AddTradeTypeLimits(tradeTypeLimits)
                .AddTradeFills(tradeFills);

            async Task<List<OptionLegDataReadModel>> GetOptionLegData(int tradeId, TradeType tradeType, DateTime valueDate, int daysToExpiry, TradeStatus tradeStatus)
                => (await db
                    .Use(StoredProcedure.spGetOptionLegData)
                    .SetParameters(new {
                        tradeId,
                        tradeType = $"{tradeType}",
                        valueDate,
                        daysToExpiry,
                        tradeStatus = $"{tradeStatus}"
                    })
                    .ExecuteQueryAsync<OptionLegDataReadModel>().ConfigureAwait(false)).ToList();
        }

        /// <summary>
        /// return all trade positions for selected trade
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradePositionReadModel>> GetTradePositionsAsync(int orderId, int tradeId)
        {
            var db = _dbFactory.TradeDb;
            var tradePositions = await db.Use(StoredProcedure.spGetTradePositions)
                 .SetParameters(new {orderId, tradeId })
                 .ExecuteQueryAsync<TradePositionReadModel>();
            if (tradePositions is not null)
            {
                var optionLegs = await GetOptionLegs();
                foreach (var e in tradePositions)
                {
                    var updatedOptionLegData = new List<OptionLegDataReadModel>();
                    var optionLegData = await GetOptionLegData(e.TradeType, e.ValueDate, e.DaysToExpiry, e.TradeStatus);
                    foreach (var old in optionLegData)
                    {
                        updatedOptionLegData.Add((new OptionLegDataReadModel(
                            TradeId: e.Key.TradeId,
                            TradeType: e.Key.TradeType,
                            ValueDate: e.Key.ValueDate,
                            DaysToExpiry: e.Key.DaysToExpiry,
                            TradeStatus: e.Key.TradeStatus,
                            OptionLegId: old.OptionLegId,
                            BidPrice: old.BidPrice,
                            AskPrice: old.AskPrice,
                            ImpliedVolatility: old.ImpliedVolatility,
                            Delta: old.Delta,
                            Gamma: old.Gamma,
                            Theta: old.Theta,
                            Vega: old.Vega,
                            Rho: old.Rho,
                            CreatedOn: old.CreatedOn, 
                            CreatedBy: old.CreatedBy, 
                            UpdatedOn: old.UpdatedOn, 
                            UpdatedBy: old.UpdatedBy)).SetOptionLeg(optionLegs.Where(o => o.ContractId == old.OptionLegId).SingleOrDefault()));
                    }
                    e.AddOptionLegData(updatedOptionLegData);
                }
            }
            return tradePositions;

            async Task<IReadOnlyList<OptionLegReadModel>> GetOptionLegs()
               => await db.Use(StoredProcedure.spGetOptionLegs)
                      .SetParameters(new { tradeId })
                      .ExecuteQueryAsync<OptionLegReadModel>();

            async Task<IReadOnlyList<OptionLegDataReadModel>> GetOptionLegData(TradeType tradeType, DateTime valueDate, int daysToExpiry, TradeStatus tradeStatus)
                => await db.Use(StoredProcedure.spGetOptionLegData)
                       .SetParameters(new {
                           tradeId,
                           tradeType = $"{tradeType}",
                           valueDate,
                           daysToExpiry,
                           tradeStatus = $"{tradeStatus}" })
                       .ExecuteQueryAsync<OptionLegDataReadModel>();
        }

        /// <summary>
        /// return trade position trade types
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="valueDate"></param>
        /// <param name="daysToExpiry"></param>
        /// <param name="tradeStatus"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<string>> GetTradePositionTradeTypesAsync(
           int orderId, int tradeId, DateTime valueDate, int daysToExpiry, TradeStatus tradeStatus)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradePositionTradeTypes)
                .SetParameters(new {
                    orderId,
                    tradeId,
                    valueDate,
                    daysToExpiry,
                    tradeStatus = $"{tradeStatus}" })
                .ExecuteQueryAsync<string>(e => e.Get("TradeType").As<string>());
        }

        /// <summary>
        /// return trade position
        /// </summary>
        /// <param name="tradeId"></param>
        /// <param name="tradeType"></param>
        /// <param name="valueDate"></param>
        /// <param name="daysToExpiry"></param>
        /// <param name="tradeStatus"></param>
        /// <returns></returns>
        public async Task<TradePositionReadModel> GetTradePositionAsync(
            int orderId,
            int tradeId,
            TradeType tradeType,
            DateTime valueDate,
            int daysToExpiry,
            TradeStatus tradeStatus)
        {
            var db = _dbFactory.TradeDb;
            var tradePosition = await db.Use(StoredProcedure.spGetTradePosition)
                .SetParameters(new {
                    orderId,
                    tradeId,
                    tradeType = $"{tradeType}",
                    valueDate,
                    daysToExpiry,
                    tradeStatus = $"{tradeStatus}" })
                .ExecuteSingleAsync<TradePositionReadModel>();
            if (tradePosition is not null)
            {
                var optionLegs = await GetOptionLegs();
                var updatedOptionLegData = new List<OptionLegDataReadModel>();
                var optionLegData = await GetOptionLegData();
                foreach (var old in optionLegData)
                {
                    updatedOptionLegData.Add((new OptionLegDataReadModel(
                            TradeId: tradePosition.Key.TradeId,
                            TradeType: tradePosition.Key.TradeType,
                            ValueDate: tradePosition.Key.ValueDate,
                            DaysToExpiry: tradePosition.Key.DaysToExpiry,
                            TradeStatus: tradePosition.Key.TradeStatus,
                            OptionLegId: old.OptionLegId,
                            BidPrice: old.BidPrice,
                            AskPrice: old.AskPrice,
                            ImpliedVolatility: old.ImpliedVolatility,
                            Delta: old.Delta,
                            Gamma: old.Gamma,
                            Theta: old.Theta,
                            Vega: old.Vega,
                            Rho: old.Rho,
                            CreatedOn: old.CreatedOn,
                            CreatedBy: old.CreatedBy,
                            UpdatedOn: old.UpdatedOn,
                            UpdatedBy: old.UpdatedBy)).SetOptionLeg(optionLegs.Where(o => o.ContractId == old.OptionLegId).SingleOrDefault()));
                }
                tradePosition.AddOptionLegData(updatedOptionLegData);
            }
            return tradePosition;

            async Task<IReadOnlyList<OptionLegReadModel>> GetOptionLegs()
                => await db.Use(StoredProcedure.spGetOptionLegs)
                       .SetParameters(new { tradeId })
                       .ExecuteQueryAsync<OptionLegReadModel>();

            async Task<IReadOnlyList<OptionLegDataReadModel>> GetOptionLegData()
                => await db.Use(StoredProcedure.spGetOptionLegData)
                       .SetParameters(new {
                           tradeId,
                           tradeType = $"{tradeType}",
                           valueDate,
                           daysToExpiry,
                           tradeStatus = $"{tradeStatus}"  })
                       .ExecuteQueryAsync<OptionLegDataReadModel>();
        }

        /// <summary>
        /// return trade history by trade order
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradeHistory)
                .SetParameters(new { orderId })
                .ExecuteQueryAsync<TradeHistoryReadModel>();
        }

        /// <summary>
        /// return trade orders by date range
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradeOrderReadModel>> GetTradeOrdersAsync(DateTime startDate, DateTime endDate)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradeOrders)
                .SetParameters(new { startDate = startDate.Date, endDate = endDate.Date })
                .ExecuteQueryAsync<TradeOrderReadModel>();
        }

        /// <summary>
        /// return list of contract ids
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<string>> GetOptionLegContractIdsAsync(int tradeId)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetOptionLegs)
                .SetParameters(new { tradeId })
                .ExecuteQueryAsync<OptionLegReadModel, string>(e => e.ContractId);
        }

        /// <summary>
        /// return trade quantity
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<int> GetTradeQuantityAsync(int tradeId)
        {
            var db = _dbFactory.TradeDb;
            var optionLegs = await db.Use(StoredProcedure.spGetOptionLegs)
                .SetParameters(new { tradeId })
                .ExecuteQueryAsync<OptionLegReadModel>();
            return optionLegs.Count > 0
                ? optionLegs.Sum(e => e.Quantity) / optionLegs.Count
                : 0;
        }

        /// <summary>
        /// return trade limit for selected trade 
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<TradeLimitReadModel> GetTradeLimitAsync(int tradeId)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradeLimit)
                    .SetParameters(new { tradeId })
                    .ExecuteSingleAsync<TradeLimitReadModel>();
        }

        /// <summary>
        /// return trade limit for selected trade 
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<TradeTypeLimitReadModel> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradeTypeLimit)
                    .SetParameters(new { tradeId, tradeType = $"{tradeType}" })
                    .ExecuteSingleAsync<TradeTypeLimitReadModel>();
        }

        /// <summary>
        /// return all trade type limit for selected trade 
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync(int tradeId)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradeTypeLimits)
                    .SetParameters(new { tradeId })
                    .ExecuteQueryAsync<TradeTypeLimitReadModel>();
        }

        /// <summary>
        /// return trade fills for selected trade 
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradeFillReadModel>> GetTradeFillsAsync(int tradeId)
        {
            var db = _dbFactory.TradeDb;
            var tradeFills = await db.Use(StoredProcedure.spGetTradeFills)
                .SetParameters(new { tradeId })
                .ExecuteQueryAsync<TradeFillReadModel>();
            if ((tradeFills?.Count ?? 0)  > 0)
                foreach(var tf in tradeFills)
                {
                    var tradeFillData = await db.Use(StoredProcedure.spGetTradeFillData)
                        .SetParameters(new { tradeId, fillDate = tf.FillDate })
                        .ExecuteQueryAsync<TradeFillDataReadModel>();
                    if ((tradeFillData?.Count ?? 0) > 0)
                        tf.AddTradeFillData(tradeFillData.ToList());
                }
            return tradeFills;
        }

        /// <summary>
        /// return trade plan
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradePlanReadModel>> GetTradePlansAsync(int orderId)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradePlan)
                    .SetParameters(new { orderId })
                    .ExecuteQueryAsync<TradePlanReadModel>();
        }

        /// <summary>
        /// return trade plan
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradePlanReadModel>> GetTradePlansAsync(int orderId, int tradeId, DateTime valueDate)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradePlans)
                    .SetParameters(new { orderId, tradeId, valueDate = valueDate.Date })
                    .ExecuteQueryAsync<TradePlanReadModel>();
        }

        /// <summary>
        /// return trade plan action
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradePlanActionReadModel>> GetTradePlanActionAsync(int orderId, int tradeId, DateTime valueDate)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradePlanAction)
                    .SetParameters(new { orderId, tradeId, valueDate = valueDate.Date })
                    .ExecuteQueryAsync<TradePlanActionReadModel>();
        }

        /// <summary>
        /// return last trade plan stop loss limit
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<TradePlanStopLossLimitReadModel> GetTradePlanStopLossLimitAsync(int orderId, int tradeId)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradePlanStopLossLimit)
                    .SetParameters(new { orderId, tradeId })
                    .ExecuteSingleAsync<TradePlanStopLossLimitReadModel>();
        }

        /// <summary>
        /// return trade plan by date range
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        public async Task<IReadOnlyList<TradePlanReadModel>> GetTradePlansAsync(DateTime startDate, DateTime endDate)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradePlanByDateRange)
                    .SetParameters(new { startDate, endDate })
                    .ExecuteQueryAsync<TradePlanReadModel>();
        }

        /// <summary>
        /// return trade plan forward loss ratios by date range
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradePlanForwardLossRatioReadModel>> GetTradePlanForwardLossRatiosAsync(DateTime startDate, DateTime endDate)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradePlanForwardLossRatio)
                    .SetParameters(new { startDate, endDate })
                    .ExecuteQueryAsync<TradePlanForwardLossRatioReadModel>();
        }

        /// <summary>
        /// return trade plan forward loss ration by vaue date
        /// </summary>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<TradePlanForwardLossRatioReadModel> GetTradePlanForwardLossRatioAsync(DateTime valueDate)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetLastTradePlanForwardLossRatio)
                    .SetParameters(new { valueDate })
                    .ExecuteSingleAsync<TradePlanForwardLossRatioReadModel>();
        }

        /// <summary>
        /// return trade plan forward loss limit by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<TradePlanForwardLossLimitReadModel> GetTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitId id)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradePlanForwardLossLimit)
                    .SetParameters(new { orderId = id.OrderId,
                        tradeId = id.TradeId,
                        tradeType = $"{id.TradeType}",
                        valueDate = id.ValueDate })
                    .ExecuteSingleAsync<TradePlanForwardLossLimitReadModel>();
        }

        /// <summary>
        /// return trade live feed
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradeLiveFeedReadModel>> GetTradeLiveFeedAsync(int orderId, int tradeId)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradeLiveFeed)
                    .SetParameters(new { orderId, tradeId })
                    .ExecuteQueryAsync<TradeLiveFeedReadModel>();
        }

        /// <summary>
        /// return trade order
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<TradeOrderReadModel> GetTradeOrderAsync(int fundId, int orderId, int tradeId)
        {
            var db = _dbFactory.TradeDb;
            return await _dbFactory.TradeDb
                    .Use(StoredProcedure.spGetTradeOrder)
                    .SetParameters(new { fundId, orderId, tradeId })
                    .ExecuteSingleAsync<TradeOrderReadModel>();
        }

        /// <summary>
        /// return trade orders by value date
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradeOrderReadModel>> GetTradeOrdersAsync(int fundId, DateTime valueDate)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradeOrderByValueDate)
                    .SetParameters(new { fundId, valueDate = valueDate.Date })
                    .ExecuteQueryAsync<TradeOrderReadModel>();
        }

        /// <summary>
        /// return trade fill data
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradeFillDataReadModel>> GetTradeFillDataAsync(int fundId, int orderId, int tradeId)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradeFillData)
                    .SetParameters(new { fundId, orderId, tradeId })
                    .ExecuteQueryAsync<TradeFillDataReadModel>();
        }

        /// return trade diary
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradeDiaryEntryReadModel>> GetTradeDiaryAsync(int orderId, int tradeId)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradeDiary)
                    .SetParameters(new { orderId, tradeId })
                    .ExecuteQueryAsync<TradeDiaryEntryReadModel>();
        }

        /// <summary>
        /// </summary>
        /// <param name="e">option trade id</param>
        /// <returns></returns>
        public async Task<TradePositionState> GetTradePositionStateAsync(OptionTradeId e)
        {
            var db = _dbFactory.TradeDb;
            return await db.Use(StoredProcedure.spGetTradePositionState)
                    .SetParameters(new { orderId = e.OrderId, tradeId = e.TradeId })
                    .ExecuteSingleAsync<TradePositionState>(o => o.Get<FieldNames>(f => f.TradePositionState).AsEnum<TradePositionState>());
        }

        /// <summary>
        /// insert option leg
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task InsertOptionLegAsync(OptionLegReadModel e)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spInsertOptionLeg)
                .SetParameters(new {
                    tradeId = e.TradeId,
                    contractId = e.ContractId,
                    quantity = e.Quantity,
                    strikePrice = e.StrikePrice,
                    optionLegType = $"{e.OptionLegType}",
                    optionLegAction = $"{e.OptionLegAction}",
                    createdOn = e.CreatedOn,
                    createdBy = e.CreatedBy,
                    updatedOn = e.UpdatedOn,
                    updatedBy = e.UpdatedBy })
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert option leg
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task InsertOptionLegDataAsync(OptionLegDataReadModel e)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spInsertOptionLegData)
                    .SetParameters(new {
                        tradeId = e.TradeId,
                        tradeType = $"{e.TradeType}",
                        valueDate = e.ValueDate,
                        daysToExpiry = e.DaysToExpiry,
                        tradeStatus = $"{e.TradeStatus}",
                        optionLegId = e.OptionLegId,
                        bidPrice = e.BidPrice,
                        askPrice = e.AskPrice,
                        impliedVolatility = e.ImpliedVolatility,
                        delta = e.Delta,
                        gamma = e.Gamma,
                        theta = e.Theta,
                        vega = e.Vega,
                        rho = e.Rho,
                        createdOn = e.CreatedOn,
                        createdBy = e.CreatedBy,
                        updatedOn = e.UpdatedOn,
                        updatedBy = e.UpdatedBy })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert option trade
        /// </summary>
        /// <param name="e">futures bar data</param>
        /// <returns></returns>
        public async Task InsertOptionTradeAsync(OptionTradeViewModel e)
        {
            // save base option trade...
            var queuedCommands = new List<object>();
            var db = _dbFactory.TradeDb;

            queuedCommands.Add(
            db.Use(StoredProcedure.spInsertOptionTrade)
                .SetParameters(new {
                    tradeId = e.TradeId,
                    orderId = e.OrderId,
                    tradeDate = e.TradeDate,
                    maturityDate = e.MaturityDate,
                    tradeType = $"{e.TradeType}",
                    tradeState = $"{e.TradeState}",
                    tradeStrategy = e.TradeStrategy ?? string.Empty,
                    tradeAction = $"{e.TradeAction}",
                    underlyingContractId = e.UnderlyingContractId,
                    underlyingAssetType = $"{e.UnderlyingAssetType}",
                    isPrimaryTrade = e.IsPrimaryTrade,
                    isHedgeTrade = e.IsHedgeTrade,
                    createdOn = e.CreatedOn,
                    createdBy = e.CreatedBy,
                    updatedOn = e.UpdatedOn,
                    updatedBy = e.UpdatedBy })
                .QueueCommand());

            // save each option leg info...
            foreach (var ol in e.OptionLegs)
                queuedCommands.Add(
                db.Use(StoredProcedure.spInsertOptionLeg)
                    .SetParameters(new {
                        tradeId = ol.TradeId,
                        contractId = ol.ContractId,
                        quantity = ol.Quantity,
                        strikePrice = ol.StrikePrice,
                        optionLegType = $"{ol.OptionLegType}",
                        optionLegAction = $"{ol.OptionLegAction}",
                        createdOn = ol.CreatedOn,
                        createdBy = ol.CreatedBy,
                        updatedOn = ol.UpdatedOn,
                        updatedBy = ol.UpdatedBy })
                    .QueueCommand());

            // save trade positions...
            foreach (var o in e.TradePositions)
            {
                queuedCommands.Add(
                db.Use(StoredProcedure.spInsertTradePosition)
                    .SetParameters(new {
                        orderId = e.OrderId,
                        tradeId = o.TradeId,
                        tradeType = $"{o.TradeType}",
                        valueDate = o.ValueDate,
                        daysToExpiry = o.DaysToExpiry,
                        tradeStatus = $"{o.TradeStatus}",
                        commission = o.Commission,
                        deltaHedge = o.DeltaHedge,
                        netSpread = o.NetSpread,
                        tradeValue = o.TradeValue,
                        tradePnl = o.TradePnl,
                        assetPrice = o.AssetPrice,
                        otmProbability = o.OTMProbability,
                        forwardPrice = o.ForwardPrice,
                        lossProbability = o.LossProbability,
                        riskFreeRate = o.RiskFreeRate,
                        createdOn = o.CreatedOn,
                        createdBy = o.CreatedBy,
                        updatedOn = o.UpdatedOn,
                        updatedBy = o.UpdatedBy })
                    .QueueCommand());

                // save option leg data within each trade position...
                foreach (var old in o.OptionLegData)
                    queuedCommands.Add(
                    db.Use(StoredProcedure.spInsertOptionLegData)
                    .SetParameters(new {
                        tradeId = old.TradeId,
                        tradeType = $"{old.TradeType}",
                        valueDate = old.ValueDate,
                        daysToExpiry = old.DaysToExpiry,
                        tradeStatus = $"{old.TradeStatus}",
                        optionLegId = old.OptionLegId,
                        bidPrice = old.BidPrice,
                        askPrice = old.AskPrice,
                        impliedVolatility = old.ImpliedVolatility,
                        delta = old.Delta,
                        gamma = old.Gamma,
                        theta = old.Theta,
                        vega = old.Vega,
                        rho = old.Rho,
                        createdOn = old.CreatedOn,
                        createdBy = old.CreatedBy,
                        updatedOn = old.UpdatedOn,
                        updatedBy = old.UpdatedBy })
                    .QueueCommand());
            }

            // save trade limit...
            queuedCommands.Add(
            db.Use(StoredProcedure.spInsertTradeLimit)
                .SetParameters(new {
                    tradeId = e.TradeLimit.TradeId,
                    tradeType = $"{e.TradeLimit.TradeType}",
                    riskMargin = e.TradeLimit.RiskMargin,
                    maxProfit = e.TradeLimit.MaxProfit,
                    maxLoss = e.TradeLimit.MaxLoss,
                    maxReturn = e.TradeLimit.MaxReturn,
                    maxLossLimit = e.TradeLimit.MaxLossLimit,
                    minProfitLimit = e.TradeLimit.MinProfitLimit,
                    maxProfitLimit = e.TradeLimit.MaxProfitLimit,
                    minProfitTarget = e.TradeLimit.MinProfitTarget,
                    dailyProfitTarget = e.TradeLimit.DailyProfitTarget,
                    createdOn = e.TradeLimit.CreatedOn,
                    createdBy = e.TradeLimit.CreatedBy,
                    updatedOn = e.TradeLimit.UpdatedOn,
                    updatedBy = e.TradeLimit.UpdatedBy })
                .QueueCommand());

            // save trade type limits...
            queuedCommands.Add(
            db.Use(StoredProcedure.spInsertTradeTypeLimit)
                .SetParameters(new {
                    tradeId = e.TradeLimit.TradeId,
                    tradeType = $"{e.TradeLimit.TradeType}",
                    maxLossLimit = e.TradeLimit.MaxLossLimit,
                    minProfitLimit = e.TradeLimit.MinProfitLimit,
                    maxProfitLimit = e.TradeLimit.MaxProfitLimit
                })
               .QueueCommand());

            foreach (var ttl in e.TradeTypeLimits)
                queuedCommands.Add(
                db.Use(StoredProcedure.spInsertTradeTypeLimit)
                    .SetParameters(new {
                        tradeId = ttl.TradeId,
                        tradeType = $"{ttl.TradeType}",
                        maxLossLimit = ttl.MaxLossLimit,
                        minProfitLimit = ttl.MinProfitLimit,
                        maxProfitLimit = ttl.MaxProfitLimit
                    })
                   .QueueCommand());

            // save any trade fills...
            foreach (var tf in e.TradeFills)
            {
                queuedCommands.Add(
                db.Use(StoredProcedure.spInsertTradeFill)
                    .SetParameters(new {
                        fundId = tf.FundId,
                        orderId = tf.OrderId,
                        tradeId = tf.TradeId,
                        fillDate = tf.FillDate,
                        fillQuantity = tf.FillQuantity,
                        createdOn = tf.CreatedOn,
                        createdBy = tf.CreatedBy })
                    .QueueCommand());

                if (tf.TradeFillData is not null)
                    foreach (var tfd in tf.TradeFillData)
                        queuedCommands.Add(
                        db.Use(StoredProcedure.spInsertTradeFillData)
                            .SetParameters(new {
                                fundId = tfd.FundId,
                                orderId = tfd.OrderId,
                                tradeId = tfd.TradeId,
                                contractId = tfd.ContractId,
                                fillDate = tfd.FillDate,
                                bidPrice = tfd.BidPrice,
                                askPrice = tfd.AskPrice,
                                commission = tfd.Commission,
                                optionLegAction = $"{tfd.OptionLegAction}",
                                createdOn = tfd.CreatedOn,
                                createdBy = tfd.CreatedBy })
                            .QueueCommand());
            }

            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }

        /// <summary>
        /// insert trade position
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task InsertTradePositionAsync(TradePositionReadModel e)
        {
            var queuedCommands = new List<object>();    
            var db = _dbFactory.TradeDb;

            queuedCommands.Add(
            db.Use(StoredProcedure.spInsertTradePosition)
                .SetParameters(new {
                    orderId = e.OrderId,
                    tradeId = e.TradeId,
                    tradeType = $"{e.TradeType}",
                    valueDate = e.ValueDate,
                    daysToExpiry = e.DaysToExpiry,
                    tradeStatus = $"{e.TradeStatus}",
                    commission = e.Commission,
                    deltaHedge = e.DeltaHedge,
                    netSpread = e.NetSpread,
                    tradeValue = e.TradeValue,
                    tradePnl = e.TradePnl,
                    assetPrice = e.AssetPrice,
                    otmProbability = e.OTMProbability,
                    forwardPrice = e.ForwardPrice,
                    forwardLossRatio = e.ForwardLossRatio,
                    lossProbability = e.LossProbability,
                    riskFreeRate = e.RiskFreeRate,
                    createdOn = e.CreatedOn,
                    createdBy = e.CreatedBy,
                    updatedOn = e.UpdatedOn,
                    updatedBy = e.UpdatedBy })
                .QueueCommand());

            // save option leg data within trade position...
            foreach (var old in e.OptionLegData)
                queuedCommands.Add(
                db.Use(StoredProcedure.spInsertOptionLegData)
                .SetParameters(new {
                    tradeId = old.TradeId,
                    tradeType = $"{old.TradeType}",
                    valueDate = old.ValueDate,
                    daysToExpiry = old.DaysToExpiry,
                    tradeStatus = $"{old.TradeStatus}",
                    optionLegId = old.OptionLegId,
                    bidPrice = old.BidPrice,
                    askPrice = old.AskPrice,
                    impliedVolatility = old.ImpliedVolatility,
                    delta = old.Delta,
                    gamma = old.Gamma,
                    theta = old.Theta,
                    vega = old.Vega,
                    rho = old.Rho,
                    createdOn = old.CreatedOn,
                    createdBy = old.CreatedBy,
                    updatedOn = old.UpdatedOn,
                    updatedBy = old.UpdatedBy })
                .QueueCommand());

            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }

        /// <summary>
        /// insert trade position
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task InsertTradePositionsAsync(ICollection<TradePositionReadModel> tradePositions)
        {
            if ((tradePositions?.Count ?? 0) == 0)
                return;

            var queuedCommands = new List<object>();    
            var db = _dbFactory.TradeDb;
            foreach (var e in tradePositions)
            {
                if (e == null)
                    continue;

                queuedCommands.Add(
                db.Use(StoredProcedure.spInsertTradePosition)
                    .SetParameters(new {
                        orderId = e.OrderId,
                        tradeId = e.TradeId,
                        tradeType = $"{e.TradeType}",
                        valueDate = e.ValueDate,
                        daysToExpiry = e.DaysToExpiry,
                        tradeStatus = $"{e.TradeStatus}",
                        commission = e.Commission,
                        deltaHedge = e.DeltaHedge,
                        netSpread = e.NetSpread,
                        tradeValue = e.TradeValue,
                        tradePnl = e.TradePnl,
                        assetPrice = e.AssetPrice,
                        otmProbability = e.OTMProbability,
                        forwardPrice = e.ForwardPrice,
                        forwardLossRatio = e.ForwardLossRatio,
                        lossProbability = e.LossProbability,
                        riskFreeRate = e.RiskFreeRate,
                        createdOn = e.CreatedOn,
                        createdBy = e.CreatedBy,
                        updatedOn = e.UpdatedOn,
                        updatedBy = e.UpdatedBy })
                    .QueueCommand());

                // save option leg data within trade position...
                foreach (var old in e.OptionLegData)
                    queuedCommands.Add(
                    db.Use(StoredProcedure.spInsertOptionLegData)
                    .SetParameters(new {
                        tradeId = old.TradeId,
                        tradeType = $"{old.TradeType}",
                        valueDate = old.ValueDate,
                        daysToExpiry = old.DaysToExpiry,
                        tradeStatus = $"{old.TradeStatus}",
                        optionLegId = old.OptionLegId,
                        bidPrice = old.BidPrice,
                        askPrice = old.AskPrice,
                        impliedVolatility = old.ImpliedVolatility,
                        delta = old.Delta,
                        gamma = old.Gamma,
                        theta = old.Theta,
                        vega = old.Vega,
                        rho = old.Rho,
                        createdOn = old.CreatedOn,
                        createdBy = old.CreatedBy,
                        updatedOn = old.UpdatedOn,
                        updatedBy = old.UpdatedBy })
                    .QueueCommand());
            }
            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }

        /// <summary>
        /// insert trade fills
        /// </summary>
        /// <param name="tradeFills"></param>
        /// <returns></returns>
        public async Task InsertTradeFillsAsync(ICollection<TradeFillReadModel> tradeFills)
        {
            // save any trade fills...
            if ((tradeFills?.Count ?? 0) > 0)
            {
                var queuedCommands = new List<object>();    
                var db = _dbFactory.TradeDb;
                foreach (var tf in tradeFills)
                {
                    queuedCommands.Add(
                    db.Use(StoredProcedure.spInsertTradeFill)
                        .SetParameters(new {
                            fundId = tf.FundId,
                            orderId = tf.OrderId,
                            tradeId = tf.TradeId,
                            fillDate = tf.FillDate,
                            fillQuantity = tf.FillQuantity,
                            createdOn = tf.CreatedOn,
                            createdBy = tf.CreatedBy })
                        .QueueCommand());

                    if ((tf.TradeFillData?.Length ?? 0) > 0)
                        foreach (var tfd in tf.TradeFillData)
                            queuedCommands.Add(
                            db.Use(StoredProcedure.spInsertTradeFillData)
                                .SetParameters(new {
                                    fundId = tfd.FundId,
                                    orderId = tfd.OrderId,
                                    tradeId = tfd.TradeId,
                                    contractId = tfd.ContractId,
                                    fillDate = tfd.FillDate,
                                    bidPrice = tfd.BidPrice,
                                    askPrice = tfd.AskPrice,
                                    commission = tfd.Commission,
                                    optionLegAction = $"{tfd.OptionLegAction}", 
                                    createdOn = tfd.CreatedOn,
                                    createdBy = tfd.CreatedBy })
                                .QueueCommand());
                }
                await db.ExecuteQueuedCommandsAsync(queuedCommands);
            }
        }

        /// <summary>
        /// insert option tradde spread data
        /// </summary>
        /// <param name="e">option trade spread data</param>
        /// <returns></returns>
        public async Task InsertOptionTradeSpreadDataAsync(OptionTradeSpreadDataViewModel e)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spInsertOptionTradeSpreadData)
                    .SetParameters(new {
                        orderId = e.OrderId,
                        tradeId = e.TradeId,
                        tradeType = $"{e.TradeType}",
                        valueDate = e.ValueDate,
                        lossLimit = e.LossLimit,
                        winLimit = e.WinLimit,
                        forwardSpread = e.ForwardSpread,
                        netSpread = e.NetSpread,
                        createdOn = e.CreatedOn,
                        createdBy = e.CreatedBy })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert option tradde spread bar data
        /// </summary>
        /// <param name="e">option trade spread bar data</param>
        /// <returns></returns>
        public async Task InsertOptionTradeSpreadBarDataAsync(OptionTradeSpreadBarDataViewModel e)
        {
            var db = _dbFactory.TradeDb;
             await db.Use(StoredProcedure.spInsertOptionTradeSpreadBarData)
                    .SetParameters(new {
                        orderId = e.OrderId,
                        tradeId = e.TradeId,
                        tradeType = $"{e.TradeType}",
                        valueDate = e.ValueDate,
                        barDate = e.BarDate,
                        lossLimit = e.LossLimit,
                        winLimit = e.WinLimit,
                        forwardSpread = e.ForwardSpread,
                        netSpread = e.NetSpread })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert trade liveFeed
        /// </summary>
        /// <param name="tradeLiveFeed"></param>
        /// <returns></returns>
        public async Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spInsertTradeLiveFeed)
                    .SetParameters(new {
                        orderId = tradeLiveFeed.OrderId,
                        tradeId = tradeLiveFeed.TradeId,
                        liveFeed = tradeLiveFeed.LiveFeed })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert trade position state
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task InsertTradePositionStateAsync(TradePositionStateReadModel e)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spInsertTradePositionState)
                    .SetParameters(new {
                        orderId = e.OrderId,
                        tradeId = e.TradeId,
                        tradePositionState = $"{e.TradePositionState}",
                        openedOn = e.OpenedOn,
                        openedBy = e.OpenedBy })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert trade diary entry
        /// </summary>
        /// <param name="e">trade diary entry</param>
        /// <returns></returns>
        public async Task InsertTradeDiaryAsync(TradeDiaryEntryReadModel e)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spInsertTradeDiary)
                    .SetParameters(new {
                        entryDate = e.EntryDate,
                        orderId = e.OrderId,
                        tradeId = e.TradeId,
                        valueDate = e.ValueDate,
                        tradeStatus = $"{e.TradeStatus}",
                        actionSource = $"{e.ActionSource}",
                        actionType = $"{e.ActionType}",
                        actionSubType = $"{e.ActionSubType}",
                        actionState = $"{e.ActionState}",
                        actionReason = e.ActionReason,
                        actionDataType = e.ActionDataType,
                        actionData = e.ActionData })
                    .ExecuteCommandAsync();
        }


        /// <summary>
        /// save trade order
        /// </summary>
        /// <param name="e">trade ticket</param>
        /// <returns></returns>
        public async Task InsertTradeOrderAsync(TradeOrderReadModel e)
        {
            var queuedCommands = new List<object>();    
            var db = _dbFactory.TradeDb;

            // save trade ticket...
            queuedCommands.Add(
            db.Use(StoredProcedure.spInsertTradeOrder)
                .SetParameters(new  {
                    fundId = e.FundId,
                    orderId = e.OrderId,
                    tradeId = e.TradeId,
                    valueDate = e.ValueDate.Date,
                    tradeType = $"{e.TradeType}",
                    tradeSubType = $"{e.TradeSubType}",
                    tradeDate = e.TradeDate.Date,
                    maturityDate = e.MaturityDate.Date,
                    tradeOrderState = $"{e.TradeOrderState}",
                    underlyingContractId = e.UnderlyingContractId,
                    underlyingAssetType = $"{e.UnderlyingAssetType}",
                    orderDescription = e.OrderDescription ?? string.Empty,
                    orderAction = $"{e.OrderAction}",
                    orderActionType = $"{e.OrderActionType}",
                    orderQuantity = e.OrderQuantity,
                    orderType = $"{e.OrderType}",
                    orderPrice = e.OrderPrice,
                    orderAmount = e.OrderAmount,
                    commission = e.Commission,
                    totalAmount = e.TotalAmount,
                    tradePnl = e.TradePnl,
                    tradeFillType = $"{e.TradeFillType}",
                    createdOn = e.CreatedOn,
                    createdBy = e.CreatedBy,
                    updatedOn = e.CreatedOn,
                    updatedBy = e.CreatedBy })
                .QueueCommand());

            // save trade fills...
            foreach (var tf in e.TradeFills)
            {
                queuedCommands.Add( 
                db.Use(StoredProcedure.spInsertTradeFill)
                    .SetParameters(new
                    {
                        fundId = tf.FundId,
                        orderId = tf.OrderId,
                        tradeId = tf.TradeId,
                        fillDate = tf.FillDate,
                        fillQuantity = tf.FillQuantity,
                        createdOn = tf.CreatedOn,
                        createdBy = tf.CreatedBy
                    })
                    .QueueCommand());

                foreach (var tfd in tf.TradeFillData)
                    queuedCommands.Add(
                    db.Use(StoredProcedure.spInsertTradeFillData)
                        .SetParameters(new
                        {
                            fundId = tfd.FundId,
                            orderId = tfd.OrderId,
                            tradeId = tfd.TradeId,
                            contractId = tfd.ContractId,
                            fillDate = tfd.FillDate,
                            bidPrice = tfd.BidPrice,
                            askPrice = tfd.AskPrice,
                            commission = tfd.Commission,
                            optionLegAction = $"{tfd.OptionLegAction}",
                            createdOn = tfd.CreatedOn,
                            createdBy = tfd.CreatedBy
                        })
                        .QueueCommand());
            }

            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }

        /// <summary>
        /// insert trade plan forward loss limit
        /// </summary>
        /// <param name="e">trade plan forward loss limit</param>
        /// <returns></returns>
        public async Task InsertTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitReadModel e)
            => await _dbFactory.TradeDb
                    .Use(StoredProcedure.spInsertTradePlanForwardLossLimit)
                    .SetParameters(new {
                        orderId = e.OrderId,
                        tradeId = e.TradeId,
                        tradeType = $"{e.TradeType}",
                        valueDate = e.ValueDate,
                        limitType = $"{e.LimitType}" })
                    .ExecuteCommandAsync();

        /// <summary>
        /// insert trade placement signal
        /// </summary>
        /// <param name="e">trade placement signal</param>
        /// <returns></returns>
        public async Task InsertTradePlacementSignalAsync(TradePlacementSignalReadModel e)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spInsertTradePlacementSignal)
                    .SetParameters(new {
                        contractId = e.ContractId,
                        valueDate = e.ValueDate.Date,
                        tradePlacementSignal = $"{e.TradePlacementSignal}",
                        tradePrice = e.TradePrice,
                        createdOn = e.CreatedOn,
                        createdBy = e.CreatedBy })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// updated trade position
        /// </summary>
        /// <param name="key"></param>
        /// <param name="commission"></param>
        /// <param name="netSpread"></param>
        /// <param name="tradeValue"></param>
        /// <param name="tradePnl"></param>
        /// <param name="assetPrice"></param>
        /// <param name="noTouchProb"></param>
        /// <param name="winRatio"></param>
        /// <param name="lossRatio"></param>
        /// <param name="durationDays"></param>
        /// <param name="riskFreeRate"></param>
        /// <param name="updatedOn"></param>
        /// <param name="updatedBy"></param>
        /// <returns></returns>
        public async Task UpdateTradePositionAsync(
            TradePositionId key,
            decimal commission,
            int deltaHedge,
            decimal netSpread,
            decimal tradeValue,
            decimal tradePnl,
            decimal assetPrice,
            double otmProbability,
            double winRatio,
            decimal maxPrice,
            double hedgeProbability,
            double riskFreeRate,
            DateTime updatedOn,
            string updatedBy)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spUpdateTradePosition)
                    .SetParameters(new {
                        tradeId = key.TradeId,
                        tradeType = $"{key.TradeType}",
                        valueDate = key.ValueDate,
                        daysToExpiry = key.DaysToExpiry,
                        tradeStatus = $"{key.TradeStatus}",
                        commission,
                        deltaHedge,
                        netSpread,
                        tradeValue,
                        tradePnl,
                        assetPrice,
                        OTMProbability = otmProbability,
                        winRatio,
                        maxPrice,
                        hedgeProbability,
                        riskFreeRate,
                        updatedOn,
                        updatedBy })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// update option trade state
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="tradeState"></param>
        /// <param name="updatedOn"></param>
        /// <param name="updatedBy"></param>
        /// <returns></returns>
        public async Task UpdateOptionTradeStateAsync(int orderId, int tradeId, TradeState tradeState, DateTime updatedOn, string updatedBy)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spUpdateOptionTradeState)
                    .SetParameters(new {
                        orderId,
                        tradeId,
                        tradeState = $"{tradeState}",
                        updatedOn,
                        updatedBy })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// update trade live feed
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name=""></param>
        /// <param name="updatedOn"></param>
        /// <param name="updatedBy"></param>
        /// <returns></returns>
        public async Task UpdateTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spUpdateTradeLiveFeed)
                    .SetParameters(new {
                        orderId = tradeLiveFeed.OrderId,
                        tradeId = tradeLiveFeed.TradeId,
                        liveFeed = tradeLiveFeed.LiveFeed })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// update trade position status
        /// </summary>
        /// <param name="tradeId"></param>
        /// <param name="tradeType"></param>
        /// <param name="valueDate"></param>
        /// <param name="daysToExpiry"></param>
        /// <param name="oldTradeStatus"></param>
        /// <param name="newTradeStatus"></param>
        /// <param name="updatedOn"></param>
        /// <param name="updatedBy"></param>
        /// <returns></returns>
        public async Task UpdateTradePositionStatusAsync(
            int tradeId,
            TradeType tradeType,
            DateTime valueDate,
            int daysToExpiry,
            TradeStatus oldTradeStatus,
            TradeStatus newTradeStatus,
            DateTime updatedOn,
            string updatedBy)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spUpdateTradePositionStatus)
                    .SetParameters(new {
                        tradeId,
                        tradeType = $"{tradeType}",
                        valueDate,
                        daysToExpiry,
                        oldTradeStatus = $"{oldTradeStatus}",
                        newTradeStatus = $"{newTradeStatus}",
                        updatedOn,
                        updatedBy })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// update option leg data
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task UpdateOptionLegDataAsync(OptionLegDataReadModel e)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spUpdateOptionLegData)
                    .SetParameters(new {
                        tradeId = e.TradeId,
                        valueDate = e.ValueDate,
                        daysToExpiry = e.DaysToExpiry,
                        tradeStatus = $"{e.TradeStatus}",
                        optionLegId = e.OptionLegId,
                        bidPrice = e.BidPrice,
                        askPrice = e.AskPrice,
                        impliedVolatility = e.ImpliedVolatility,
                        delta = e.Delta,
                        gamma = e.Gamma,
                        theta = e.Theta,
                        vega = e.Vega,
                        rho = e.Rho,
                        updatedOn = e.UpdatedOn,
                        updatedBy = e.UpdatedBy })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// update trade order state
        /// </summary>
        /// <param name="tradeOrderId"></param>
        /// <param name="tradeOrderState"></param>
        /// <param name="updatedOn"></param>
        /// <param name="updatedBy"></param>
        /// <returns></returns>
        public async Task UpdateTradeOrderStateAsync(TradeOrderId tradeOrderId, TradeOrderState tradeOrderState, DateTime updatedOn, string updatedBy)
        { 
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spUpdateTradeOrderState)
                    .SetParameters(new {
                        fundId = tradeOrderId.FundId,
                        orderId = tradeOrderId.OrderId,
                        tradeId = tradeOrderId.TradeId,
                        tradeOrderState = $"{tradeOrderState}",
                        updatedOn,
                        updatedBy })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// update trade order order price
        /// </summary>
        /// <param name="tradeOrderId"></param>
        /// <param name="orderPrice"></param>
        /// <param name="updatedOn"></param>
        /// <param name="updatedBy"></param>
        /// <returns></returns>
        public async Task UpdateTradeOrderOrderPriceAsync(TradeOrderId tradeOrderId, decimal orderPrice, DateTime updatedOn, string updatedBy)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spUpdateTradeOrderOrderPrice)
                    .SetParameters(new {
                        fundId = tradeOrderId.FundId,
                        orderId = tradeOrderId.OrderId,
                        tradeId = tradeOrderId.TradeId,
                        orderPrice,
                        updatedOn,
                        updatedBy })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert trade limit for selected trade
        /// </summary>
        /// <param name="e">trade limit</param>
        /// <returns></returns>
        public async Task InsertTradeLimitAsync(TradeLimitReadModel e)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spInsertTradeLimit)
                .SetParameters(new  {
                    tradeId = e.TradeId,
                    tradeType = $"{e.TradeType}",
                    riskMargin = e.RiskMargin,
                    maxProfit = e.MaxProfit,
                    maxReturn = e.MaxReturn,
                    maxLossLimit = e.MaxLossLimit,
                    minProfitLimit = e.MinProfitLimit,
                    maxProfitLimit = e.MaxProfitLimit,
                    minProfitTarget = e.MinProfitTarget,
                    dailyProfitTartget = e.DailyProfitTarget,
                    createdOn = e.CreatedOn,
                    createdBy = e.CreatedBy,
                    updatedOn = e.UpdatedOn,
                    updatedBy = e.UpdatedBy  })
               .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert trade limit for selected trade
        /// </summary>
        /// <param name="e">trade limit</param>
        /// <returns></returns>
        public async Task InsertTradeTypeLimitAsync(TradeTypeLimitReadModel e)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spInsertTradeTypeLimit)
                .SetParameters(new {
                    tradeId = e.TradeId,
                    tradeType = $"{e.TradeType}",
                    maxLossLimit = e.MaxLossLimit,
                    minProfitLimit = e.MinProfitLimit,
                    maxProfitLimit = e.MaxProfitLimit })
               .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert trade plan
        /// </summary>
        /// <param name="e">trade plan</param>
        /// <returns></returns>
        public async Task InsertTradePlanAsync(TradePlanReadModel e)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spInsertTradePlan)
                .SetParameters(new {
                    tradePlanId = $"{e.TradePlanId}",
                    orderId = e.OrderId,
                    tradeId = e.TradeId,
                    tradeType = $"{e.TradeType}",
                    tradeDate = e.TradeDate,
                    valueDate = e.ValueDate,
                    maturityDate = e.MaturityDate,
                    actionDate = e.ActionDate,
                    actionType = $"{e.ActionType}",
                    actionSubType = $"{e.ActionSubType}",
                    actionState = $"{e.ActionState}",
                    actionReason = e.ActionReason,
                    tradePnl = e.TradePnl,
                    forwardLossRatio = e.ForwardLossRatio,
                    lossProbability = e.LossProbability,
                    mscore = e.MScore,
                    maxProfit = e.MaxProfit,
                    maxLoss = e.MaxLoss,
                    minProfitTarget = e.MinProfitTarget,
                    dailyProfitTarget = e.DailyProfitTarget,
                    assetPrice = e.AssetPrice,
                    assetStdDev = e.AssetStdDev,
                    assetMean = e.AssetMean,
                    assetPriceChange = e.AssetPriceChange,
                    marketTrend = $"{e.MarketTrend}",
                    marketVolatility = $"{e.MarketVolatility}",
                    marketDirection = $"{e.MarketDirection}",
                    vixVolatility = $"{e.VixVolatility}",
                    tradeRisk = $"{e.TradeRisk}",
                    fiftyDayMA = e.FiftyDayMA,
                    fiveDayXMA = e.FiveDayXMA,
                    putOTMProbability = e.PutOTMProbability,
                    callOTMProbability = e.CallOTMProbability,
                    shortPutGamma = e.ShortPutGamma,
                    shortCallGamma = e.ShortCallGamma,
                    gammaRisk = $"{e.GammaRisk}",
                    netPrice = e.NetPrice,
                    forwardPrice = e.ForwardPrice,
                    forwardDelta = e.ForwardDelta,
                    stopLossLimit = e.StopLossLimit,
                    trendType = $"{e.TrendType}",
                    trendStrength = $"{e.TrendStrength}",
                    rsi = e.RSI,
                    rsiSlope = e.RSISlope,
                    tdi = $"{e.TDI}",
                    tdiStrength = $"{e.TDIStrength}",
                    createdOn = e.CreatedOn,
                    createdBy = e.CreatedBy })
               .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert trade plan action
        /// </summary>
        /// <param name="e">trade plan action</param>
        /// <returns></returns>
        public async Task InsertTradePlanActionAsync(TradePlanActionReadModel e)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spInsertTradePlanAction)
                .SetParameters(new {
                    tradePlanId = e.TradePlanId,
                    orderId = e.OrderId,
                    tradeId = e.TradeId,
                    valueDate = e.ValueDate,
                    actionType = $"{e.ActionType}",
                    actionSubType = $"{e.ActionSubType}",
                    actionState = $"{e.ActionState}",
                    actionDate = e.ActionDate,
                    actionReason = e.ActionReason,
                    createdOn = e.CreatedOn,
                    createdBy = e.CreatedBy })
               .ExecuteCommandAsync();
        }

        /// <summary>
        /// delete selected trade position state
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task DeleteTradePositionStateAsync(OptionTradeId e)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spDeleteTradePositionState)
                .SetParameters(new { orderId = e.OrderId, tradeId = e.TradeId })
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// delete selected trade type limit
        /// </summary>
        /// <param name="tradeId"></param>
        /// <param name="tradeType"></param>
        /// <returns></returns>
        public async Task DeleteTradeTypeLimitAsync(int tradeId, TradeType tradeType)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spDeleteTradeTypeLimit)
                    .SetParameters(new { tradeId, tradeType = $"{tradeType}" })
                    .ExecuteCommandAsync();
        }

        /// <summary>
        /// delete trade
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task DeleteOptionTradeAsync(int tradeId)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spDeleteOptionTrade)
                  .SetParameters(new { tradeId })
                  .ExecuteCommandAsync();
        }

        /// <summary>
        /// delete trade
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="tradeType"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task DeleteOptionTradeSpreadBarDataAsync(int orderId, int tradeId, TradeType tradeType, DateTime valueDate)
        {
            var db = _dbFactory.TradeDb;
             await db.Use(StoredProcedure.spDeleteOptionTradeSpreadBarData)
                  .SetParameters(new {
                      orderId,
                      tradeId,
                      tradeType = $"{tradeType}",
                      valueDate })
                  .ExecuteCommandAsync();
        }

        /// <summary>
        /// delete trade live feed
        /// </summary>
        /// <param name="tradeId"></param>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task DeleteTradeLiveFeedAsync(int orderId, int tradeId)
        {
            var db = _dbFactory.TradeDb;
             await db.Use(StoredProcedure.spDeleteTradeLiveFeed)
                  .SetParameters(new { orderId, tradeId })
                  .ExecuteCommandAsync();
        }

        /// <summary>
        /// delete all trade live feeds for order
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task DeleteTradeLiveFeedsAsync(int orderId)
        {
             var db = _dbFactory.TradeDb;
             await db.Use(StoredProcedure.spDeleteTradeLiveFeeds)
                  .SetParameters(new { orderId })
                  .ExecuteCommandAsync();
        }

        /// <summary>
        /// delete trade plan forward loss limit
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task DeleteTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitId id)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spDeleteTradePlanForwardLossLimit)
                  .SetParameters(new { id.OrderId,
                      id.TradeId,
                      tradeType = $"{id.TradeType}",
                      id.ValueDate })
                  .ExecuteCommandAsync();
        }

        /// <summary>
        /// update trade limit daily profit target
        /// </summary>
        /// <param name="tradeId"></param>
        /// <param name="tradeType"></param>
        /// <param name="dailyProfitTarget"></param>
        /// <param name="updatedOn"></param>
        /// <param name="updatedBy"></param>
        /// <returns></returns>
        public async Task UpdateTradeLimitDailyProfitTarget(int tradeId, TradeType tradeType, decimal dailyProfitTarget, DateTime updatedOn, string updatedBy)
        {
            var db = _dbFactory.TradeDb;
             await db.Use(StoredProcedure.spUpdateTradeLimitDailyProfitTarget)
                .SetParameters(new {
                    tradeId,
                    tradeType = $"{tradeType}",
                    dailyProfitTarget,
                    updatedOn,
                    updatedBy })
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// backup trade database
        /// </summary>
        /// <param name="backupType"></param>
        /// <returns></returns>
        public async Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage)
        {
            var db = _dbFactory.TradeDb;
            await db.Use(StoredProcedure.spBackupDatabase)
                .SetParameters(new { backupType = $"{backupType}" })
                .WithNoTransaction()
                .SetCommandTimeout(commandTimeout)
                .ExecuteCommandAsync(onInfoMessage);
        }
        
    }

}
