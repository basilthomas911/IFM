using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Blackboard;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Application.Event.SignalR.Services
{
    public class BlackboardService : IBlackboardService
    {
        private readonly IDataCacheService _dataCacheService;
        private readonly IMarketDataFeedQueryApi _marketDataFeedQueryApi;

        public BlackboardService(IDataCacheService dataCacheService, IMarketDataFeedQueryApi marketDataFeedQueryApi)
        {
            _dataCacheService = dataCacheService;
            _marketDataFeedQueryApi = marketDataFeedQueryApi;
        }

        /// <summary>
        /// return current trade position action
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public TradePositionActionReadModel GetTradePositionAction(TradePositionId id)
        {
            var tradePositionAction = default(TradePositionActionReadModel);
            if (_dataCacheService.Exists<TradePositionId>(DataCacheName.TradePositionAction, id))
                tradePositionAction = _dataCacheService.Get<TradePositionId, TradePositionActionReadModel>(DataCacheName.TradePositionAction, id);
            return tradePositionAction;
        }

        /// <summary>
        /// update current trade position action
        /// </summary>
        /// <param name="id"></param>
        /// <param name="tpa"></param>
        public void SetTradePositionAction(TradePositionId id, TradePositionActionReadModel tpa)
        {
            _dataCacheService.Update<TradePositionId, TradePositionActionReadModel>(DataCacheName.TradePositionAction, id, tpa);
        }

        /// <summary>
        /// return current hedge position trade id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public OptionTradeId GetHedgePositionTradeId(TradePositionId id)
        {
            var optionTradeId = default(OptionTradeId);
            if (_dataCacheService.Exists<TradePositionId>(DataCacheName.HedgePositionTradeId, id))
                optionTradeId = _dataCacheService.Get<TradePositionId, OptionTradeId>(DataCacheName.HedgePositionTradeId, id);
            return optionTradeId;
        }

        /// <summary>
        /// update current hedge position trade id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="optionTradeId"></param>
        public void SetHedgePositionTradeId(TradePositionId id, OptionTradeId optionTradeId)
        {
            _dataCacheService.Update<TradePositionId, OptionTradeId>(DataCacheName.HedgePositionTradeId, id, optionTradeId);
        }

        /// <summary>
        /// return cached futures tick data
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public FuturesTickDataViewModel GetFuturesTickData(string contractId, DateTime valueDate)
        {
            var futuresTickData = default(FuturesTickDataViewModel);
            var cacheId = FuturesTickDataId.Create(contractId, valueDate);
            if (_dataCacheService.Exists<FuturesTickDataId>(DataCacheName.FuturesTickData, cacheId))
                futuresTickData = _dataCacheService.Get<FuturesTickDataId, FuturesTickDataViewModel>(DataCacheName.FuturesTickData, cacheId);
            return futuresTickData;
        }

        /// <summary>
        /// cache futures tick data
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="futuresTickData"></param>
        public void SetFuturesTickData(string contractId, DateTime valueDate, FuturesTickDataViewModel futuresTickData)
        {
            var cacheId = FuturesTickDataId.Create(contractId, valueDate);
            _dataCacheService.Update<FuturesTickDataId, FuturesTickDataViewModel>(DataCacheName.FuturesTickData, cacheId, futuresTickData);
        }

        public FuturesEodDataViewModel GetFuturesEodData(string contractId, DateTime valueDate)
        {
            var futuresEodData= default(FuturesEodDataViewModel);
            var cacheId = FuturesEodDataId.Create(contractId, valueDate);
            if (_dataCacheService.Exists<FuturesEodDataId>(DataCacheName.FuturesEodData, cacheId))
                futuresEodData = _dataCacheService.Get<FuturesEodDataId, FuturesEodDataViewModel>(DataCacheName.FuturesEodData, cacheId);
            return futuresEodData;
        }

        public void SetFuturesEodData(string contractId, DateTime valueDate, FuturesEodDataViewModel futuresEodData)
        {
            var cacheId = FuturesEodDataId.Create(contractId, valueDate);
            _dataCacheService.Update<FuturesEodDataId, FuturesEodDataViewModel>(DataCacheName.FuturesEodData, cacheId, futuresEodData);
        }

        /// <summary>
        /// return futures eod data by date range
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public FuturesEodDataViewModel[] GetFuturesEodDataRange(string contractId, DateTime valueDate)
        {
            var futuresEodData = default(FuturesEodDataViewModel[]);
            var cacheId = FuturesEodDataId.Create(contractId, valueDate);
            if (!_dataCacheService.Exists<FuturesEodDataId>(DataCacheName.FuturesEodDataRange, cacheId))
            {
                var startDate = valueDate.AddMonths(-2);
                var endDate = valueDate.AddDays(-1);
                var serviceResult = _marketDataFeedQueryApi.GetFuturesEodDataAsync(contractId, startDate, endDate).Result;
                if (serviceResult.Success)
                {
                    futuresEodData = serviceResult.Value;
                    _dataCacheService.Update<FuturesEodDataId, FuturesEodDataViewModel[]>(DataCacheName.FuturesEodDataRange, cacheId, futuresEodData);
                }
            }
            futuresEodData = _dataCacheService.Get<FuturesEodDataId, FuturesEodDataViewModel[]>(DataCacheName.FuturesEodDataRange, cacheId);
            return futuresEodData;
        }

        public void RemoveFuturesEodDataRange(string contractId, DateTime valueDate)
        {
            var cacheId = FuturesEodDataId.Create(contractId, valueDate);
            if (!_dataCacheService.Exists<FuturesEodDataId>(DataCacheName.FuturesEodDataRange, cacheId))
                _dataCacheService.Remove<FuturesEodDataId>(DataCacheName.FuturesEodDataRange, cacheId);
        }

        public NormalCurveTableReadModel GetNormalCurveTable(DateTime valueDate)
        {
            var normalCurveTable = default(NormalCurveTableReadModel);
            var cacheId = valueDate.ToString("yyyy-MM-dd");
            if (!_dataCacheService.Exists<string>(DataCacheName.NormalCurveTable, cacheId))
            {
                var serviceResult = _marketDataFeedQueryApi.GetNormalCurveTableAsync().Result;
                if (serviceResult.Success)
                {
                    normalCurveTable = serviceResult.Value;
                    _dataCacheService.Update<string, NormalCurveTableReadModel>(DataCacheName.NormalCurveTable, cacheId, normalCurveTable);
                }
            }
            normalCurveTable = _dataCacheService.Get<string, NormalCurveTableReadModel>(DataCacheName.NormalCurveTable, cacheId);
            return normalCurveTable;
        }

        public void RemoveNormalCurveTable(DateTime valueDate)
        {
            var cacheId = valueDate.ToString("yyyy-MM-dd");
            if (!_dataCacheService.Exists<string>(DataCacheName.NormalCurveTable, cacheId))
                _dataCacheService.Remove<string>(DataCacheName.NormalCurveTable, cacheId);
        }
    }
}
