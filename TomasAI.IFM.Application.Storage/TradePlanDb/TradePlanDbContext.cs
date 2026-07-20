using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradePlan.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Framework.Storage;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Application.Storage.TradePlanDb
{
    public class TradePlanDbContext : ObjectDataRepository<TradePlanDbContext>, ITradePlanDbContext, ITradePlanDbReadContext, ITradePlanDbWriteContext
    {
        IDbContextFactory _dbFactory;

        /// <summary>
        /// trade plan database constructor
        /// </summary>
        /// <param name="connectionSettings"></param>
        public TradePlanDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<TradePlanDbContext> logger)
            : base(connectionSettings[TradePlanDbConnection], logger)
        {
            _dbFactory = dbFactory;
        }

        public const string TradePlanDbConnection = "TradePlanDbConnection";

        /// <summary>
        /// return db reader/writer properties
        /// </summary>
        public ITradePlanDbReadContext DbReader => this;
        public ITradePlanDbWriteContext DbWriter => this;

        public DbMap<IronCondorTradePlanReadModel> IronCondorTradePlan { get; private set; }
        public DbMap<TradePlanForwardLossRatioReadModel> TradePlanForwardLossRatio { get; private set; }
        public DbMap<TradePlanStopLossLimitReadModel> TradePlanStopLossLimit { get; private set; }

        public enum StoredProcedure
        {
            spBackupDatabase,
            spGetIronCondorTradePlans,
            spGetIronCondorTradePlanByDateRange,
            spGetIronCondorTradePlanForwardLossRatio,
            spGetLastIronCondorTradePlanForwardLossRatio,
            spGetIronCondorTradePlanStopLossLimit,
            spInsertIronCondorTradePlan
        }

        public class FieldNames
        {
            public readonly string TradePositionState;
        }

        /// <summary>
        /// initialize trade plan model mappings
        /// </summary>
        /// <param name="model"></param>
        public override void OnCreateModel(DbModel<TradePlanDbContext> model)
        {

            IronCondorTradePlan = Map(e => e.IronCondorTradePlan)
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
                     .Set(o => o.StopLossLimit)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                );


            TradePlanForwardLossRatio = Map(e => e.TradePlanForwardLossRatio)
                .Parameters(e =>
                    e.Set(o => o.ForwardLossRatio)
                );

            TradePlanStopLossLimit = Map(e => e.TradePlanStopLossLimit)
                .Parameters(e =>
                    e.Set(o => o.StopLossLimit)
                );

        }
      
        /// <summary>
        /// return iron condor trade plans
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<IronCondorTradePlanReadModel>> GetIronCondorTradePlansAsync(int orderId, int tradeId, DateTime valueDate)
            => await _dbFactory.TradePlanDb
                .Use(StoredProcedure.spGetIronCondorTradePlans)
                .SetParameters(new { orderId, tradeId, valueDate = valueDate.Date })
                .ExecuteQueryAsync<IronCondorTradePlanReadModel>();

        /// <summary>
        /// return last iron condor trade plan stop loss limit
        /// </summary>
        /// <returns></returns>
        public async Task<TradePlanStopLossLimitReadModel> GetIronCondorTradePlanStopLossLimitAsync(int orderId, int tradeId)
            => await _dbFactory.TradePlanDb
                .Use(StoredProcedure.spGetIronCondorTradePlanStopLossLimit)
                .SetParameters(new { orderId, tradeId})
                .ExecuteSingleAsync<TradePlanStopLossLimitReadModel>();

        /// <summary>
        /// return iron condors trade plan by date range
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        public async Task<IReadOnlyList<IronCondorTradePlanReadModel>> GetIronCondorTradePlansAsync(DateTime startDate, DateTime endDate)
            => await _dbFactory.TradePlanDb
                .Use(StoredProcedure.spGetIronCondorTradePlanByDateRange)
                .SetParameters(new { startDate, endDate })
                .ExecuteQueryAsync<IronCondorTradePlanReadModel>();

        /// <summary>
        /// return iron condor trade plan forward loss ratios by date range
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradePlanForwardLossRatioReadModel>> GetIronCondorTradePlanForwardLossRatiosAsync(DateTime startDate, DateTime endDate)
            => await _dbFactory.TradePlanDb
                .Use(StoredProcedure.spGetIronCondorTradePlanForwardLossRatio)
                .SetParameters(new { startDate, endDate })
                .ExecuteQueryAsync<TradePlanForwardLossRatioReadModel>();

        /// <summary>
        /// return iron condor trade plan forward loss ration by vaue date
        /// </summary>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<TradePlanForwardLossRatioReadModel> GetIronCondorTradePlanForwardLossRatioAsync(DateTime valueDate)
            => await _dbFactory.TradePlanDb
                .Use(StoredProcedure.spGetLastIronCondorTradePlanForwardLossRatio)
                .SetParameters(new { valueDate })
                .ExecuteSingleAsync<TradePlanForwardLossRatioReadModel>();

        /// <summary>
        /// insert iron condor trade plan
        /// </summary>
        /// <param name="e">iron condor trade plan</param>
        /// <returns></returns>
        public async Task InsertIronCondorTradePlanAsync(IronCondorTradePlanReadModel e)
            => await _dbFactory.TradePlanDb
                .Use(StoredProcedure.spInsertIronCondorTradePlan)
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
                    stopLossLimit = e.StopLossLimit,
                    createdOn = e.CreatedOn,
                    createdBy = e.CreatedBy })
               .ExecuteCommandAsync();

        /// <summary>
        /// backup trade plan database
        /// </summary>
        /// <param name="backupType"></param>
        /// <returns></returns>
        public async Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage)
            => await _dbFactory.TradePlanDb
                .Use(StoredProcedure.spBackupDatabase)
                .SetParameters(new {backupType = $"{backupType}"})
                .WithNoTransaction()
                .SetCommandTimeout(commandTimeout)
                .ExecuteCommandAsync(onInfoMessage);
        
    }

}
