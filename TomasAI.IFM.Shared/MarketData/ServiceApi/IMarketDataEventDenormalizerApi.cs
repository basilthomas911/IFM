using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Shared.MarketData.ServiceApi
{
    public interface IMarketDataEventDenormalizerApi
    {
        Task InsertFuturesContractAsync(FuturesContractAddedEvent e);
        Task DeleteFuturesContractAsync(FuturesContractRemovedEvent e);
        Task UpdateFuturesContractAsync(FuturesContractChangedEvent e);
        Task InsertFuturesOptionContractAsync(FuturesOptionContractAddedEvent e);
        Task DeleteFuturesOptionContractAsync(FuturesOptionContractRemovedEvent e);
        Task UpdateFuturesOptionContractAsync(FuturesOptionContractChangedEvent e);
        Task InsertYieldCurveRateAsync(YieldCurveRateAddedEvent e);
        Task UpdateYieldCurveRateAsync(YieldCurveRateChangedEvent e);
        Task DeleteYieldCurveRateAsync(YieldCurveRateRemovedEvent e);
        Task InsertYieldCurveRatesAsync(YieldCurveRatesImportedEvent e);
    }
}
