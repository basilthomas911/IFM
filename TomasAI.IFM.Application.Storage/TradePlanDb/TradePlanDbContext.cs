using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared;
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

        public enum StoredProcedure
        {
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

        static IronCondorTradePlanReadModel MapTradePlan(IObjectDataRecord row)
            => new(
                row.GetGuid(0), row.GetInt(1), row.GetInt(2), row.GetEnum<TradeType>(3),
                row.GetDateTime(4), row.GetDateOnly(5), row.GetDateTime(6), row.GetDateTime(7),
                row.GetEnum<ActionType>(8), row.GetEnum<ActionSubType>(9), row.GetEnum<ActionState>(10),
                row.GetString(11), row.GetDecimal(12), row.GetDouble(13), row.GetDouble(14),
                row.GetDouble(15), row.GetDecimal(16), row.GetDecimal(17), row.GetDecimal(18),
                row.GetDecimal(19), row.GetDecimal(20), row.GetDouble(21), row.GetDouble(22),
                row.GetDouble(23), row.GetEnum<MarketDirectionType>(24),
                row.GetEnum<MarketVolatilityType>(25), row.GetEnum<PriceDirectionType>(26),
                row.GetEnum<PriceVolatilityType>(27), row.GetEnum<TradeRiskType>(28),
                row.GetDouble(29), row.GetDouble(30), row.GetDouble(31), row.GetDouble(32),
                row.GetDouble(33), row.GetDouble(34), row.GetEnum<GammaRiskType>(35),
                row.GetDecimal(36), row.GetDecimal(37), row.GetDouble(38),
                row.GetDateTime(39), row.GetString(40));

        static TradePlanForwardLossRatioReadModel MapForwardLossRatio(IObjectDataRecord row)
            => new(row.GetDouble(0));

        static TradePlanStopLossLimitReadModel MapStopLossLimit(IObjectDataRecord row)
            => new(row.GetDouble(0));
      
        /// <summary>
        /// return iron condor trade plans
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<IronCondorTradePlanReadModel>> GetIronCondorTradePlansAsync(int orderId, int tradeId, DateTime valueDate)
            => (await _dbFactory.TradePlanDb
                .Use(StoredProcedure.spGetIronCondorTradePlans)
                .SetParameters(new { orderId, tradeId, valueDate = valueDate.Date })
                .ExecuteQueryAsync(MapTradePlan)).ToArray();

        /// <summary>
        /// return last iron condor trade plan stop loss limit
        /// </summary>
        /// <returns></returns>
        public async Task<TradePlanStopLossLimitReadModel> GetIronCondorTradePlanStopLossLimitAsync(int orderId, int tradeId)
            => await _dbFactory.TradePlanDb
                .Use(StoredProcedure.spGetIronCondorTradePlanStopLossLimit)
                .SetParameters(new { orderId, tradeId})
                .ExecuteSingleAsync(MapStopLossLimit);

        /// <summary>
        /// return iron condors trade plan by date range
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        public async Task<IReadOnlyList<IronCondorTradePlanReadModel>> GetIronCondorTradePlansAsync(DateTime startDate, DateTime endDate)
            => (await _dbFactory.TradePlanDb
                .Use(StoredProcedure.spGetIronCondorTradePlanByDateRange)
                .SetParameters(new { startDate, endDate })
                .ExecuteQueryAsync(MapTradePlan)).ToArray();

        /// <summary>
        /// return iron condor trade plan forward loss ratios by date range
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TradePlanForwardLossRatioReadModel>> GetIronCondorTradePlanForwardLossRatiosAsync(DateTime startDate, DateTime endDate)
            => (await _dbFactory.TradePlanDb
                .Use(StoredProcedure.spGetIronCondorTradePlanForwardLossRatio)
                .SetParameters(new { startDate, endDate })
                .ExecuteQueryAsync(MapForwardLossRatio)).ToArray();

        /// <summary>
        /// return iron condor trade plan forward loss ration by vaue date
        /// </summary>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<TradePlanForwardLossRatioReadModel> GetIronCondorTradePlanForwardLossRatioAsync(DateTime valueDate)
            => await _dbFactory.TradePlanDb
                .Use(StoredProcedure.spGetLastIronCondorTradePlanForwardLossRatio)
                .SetParameters(new { valueDate })
                .ExecuteSingleAsync(MapForwardLossRatio);

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

    }

}
