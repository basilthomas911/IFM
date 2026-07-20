using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.WebApi;
using TomasAI.IFM.Shared.WebService;

namespace TomasAI.IFM.Application.WebApiClient.MarketData
{
    public class MarketDataQueryApi : RestApiService, IMarketDataQueryApi
    {
        public MarketDataQueryApi(IWebConnectionSettings connectionSettings)
            : this(connectionSettings["MarketDataQueryApi"])
        {
        }

        private MarketDataQueryApi(IWebConnectionSetting connectionSetting)
            : base(new Uri(connectionSetting.BaseUri))
        {
        }

        public FuturesContractViewModel GetFuturesContract(string contractId)
            => ExecuteRestApi<FuturesContractViewModel>( e => {
                var result = Get<FuturesContractViewModel>(
                    getUri: $"MarketData/Futures/Contract/{contractId}");
                if (!result.Success)
                    throw new ServiceException(result.ErrorCode, result.ErrorMessage);
                e.ReturnValue = result.Value;
            });


        public FuturesOptionContractReadModel GetFuturesOptionContract(string contractId)
           => ExecuteRestApi<FuturesOptionContractReadModel>( e => {
               var result = Get<FuturesOptionContractReadModel>(
                   getUri: $"MarketData/FuturesOption/Contract/{contractId}");
               if (!result.Success)
                   throw new ServiceException(result.ErrorCode, result.ErrorMessage);
               e.ReturnValue = result.Value;
           });

        public FuturesOptionContractReadModel[] GetFuturesOptionContracts(string symbol)
           => ExecuteRestApi<FuturesOptionContractReadModel[]>(e => {
               var result = Get<FuturesOptionContractReadModel[]>(
                   getUri: $"MarketData/FuturesOption/Contract?symbol={symbol}");
               if (!result.Success)
                   throw new ServiceException(result.ErrorCode, result.ErrorMessage);
               e.ReturnValue = result.Value;
           });

        public YieldCurveRateReadModel GetLastYieldCurveRate()
            => ExecuteRestApi<YieldCurveRateReadModel>(e => {
                var result = Get<YieldCurveRateReadModel>(
                    getUri: $"MarketData/YieldCurveRate");
                if (!result.Success)
                    throw new ServiceException(result.ErrorCode, result.ErrorMessage);
                e.ReturnValue = result.Value;
            });

        public ReturnRateViewModel GetLastRateOfReturn(string symbol)
            => ExecuteRestApi<ReturnRateViewModel>( e => {
                var result = Get<ReturnRateViewModel>(
                    getUri: $"MarketData/RateOfReturn?symbol={symbol}");
                if (!result.Success)
                    throw new ServiceException(result.ErrorCode, result.ErrorMessage);
                e.ReturnValue = result.Value;
            });

        public int GetTradingDays(DateTime startDate, DateTime endDate, MarketType marketType, CurrencyType currencyType)
            => ExecuteRestApi<int>( e => {
                var result =  Get<int>(
                    getUri: $"MarketData/TradingDays?startDate={startDate.ToString("yyyyMMdd")}&endDate={endDate.ToString("yyyyMMdd")}&marketType={marketType.ToString()}&currencyType={currencyType.ToString()}");
                if (!result.Success)
                    throw new ServiceException(result.ErrorCode, result.ErrorMessage);
                e.ReturnValue = result.Value;
            });
    }
}
