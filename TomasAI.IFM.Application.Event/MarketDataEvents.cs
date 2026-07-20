using System;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;

namespace TomasAI.IFM.Application.Event
{
    public class MarketDataEvents : BaseEvents,
        IAsyncEventHandler<FuturesContractAddedEvent>,
        IAsyncEventHandler<FuturesContractRemovedEvent>,
        IAsyncEventHandler<FuturesContractChangedEvent>,
        IAsyncEventHandler<FuturesOptionContractAddedEvent>,
        IAsyncEventHandler<FuturesOptionContractRemovedEvent>,
        IAsyncEventHandler<FuturesOptionContractChangedEvent>,
        IAsyncEventHandler<YieldCurveRateAddedEvent>,
        IAsyncEventHandler<YieldCurveRateRemovedEvent>,
        IAsyncEventHandler<YieldCurveRateChangedEvent>,
        IAsyncEventHandler<YieldCurveRatesImportedEvent>
    {
        private readonly IStatusConsoleServiceApi _statusConsoleLog;
        private readonly IMarketDataEventDenormalizerApi _marketDataDenormalizer;
        private const int ErrorCodeBase = 2000;

        public MarketDataEvents( IStatusConsoleServiceApi statusConsoleLog, IMarketDataEventDenormalizerApi marketDataDenormalizer)
            :base(statusConsoleLog)
        {
            _statusConsoleLog = statusConsoleLog;
            _marketDataDenormalizer = marketDataDenormalizer;
        }

        /// <summary>
        /// execute futures contract created event handler
        /// </summary>
        /// <param name="e">futures contract created event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesContractAddedEvent e)
            => await ExecuteAsync(ErrorCodeBase, async () => {
                await _marketDataDenormalizer.InsertFuturesContractAsync(e);
                await WriteConsoleAsync(StatusSourceType.MarketData, $"FuturesContractAdded: {e.Contract.ContractId}");
            });


        /// <summary>
        /// execute futures contract deleted event
        /// </summary>
        /// <param name="e">futures contract deleted event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesContractRemovedEvent e)
             => await ExecuteAsync(ErrorCodeBase+1, async () => {
                 await _marketDataDenormalizer.DeleteFuturesContractAsync(e);
                 await WriteConsoleAsync($"FuturesContractRemoved: {e.ContractId}");
             });

        /// <summary>
        /// executue futures contract changed event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesContractChangedEvent e)
             => await ExecuteAsync(ErrorCodeBase + 2, async () => {
                 await _marketDataDenormalizer.UpdateFuturesContractAsync(e);
                 await WriteConsoleAsync($"FuturesContractChanged: {e.Contract.ContractId}");
             });

        /// <summary>
        /// execute futures option contract added event handler
        /// </summary>
        /// <param name="e">futures option contract added event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesOptionContractAddedEvent e)
            => await ExecuteAsync(ErrorCodeBase + 3, async () => {
                await _marketDataDenormalizer.InsertFuturesOptionContractAsync(e);
                await WriteConsoleAsync($"FuturesOptionContractAdded: {e.Contract.ContractId}");
            });

        /// <summary>
        /// execute futures option contract deleted event
        /// </summary>
        /// <param name="e">futures option contract deleted event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesOptionContractRemovedEvent e)
             => await ExecuteAsync(ErrorCodeBase + 3, async () => {
                 await _marketDataDenormalizer.DeleteFuturesOptionContractAsync(e);
                 await WriteConsoleAsync($"FuturesOptionContractRemoved: {e.ContractId}");
             });

        /// <summary>
        /// execute futures contract changed event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(FuturesOptionContractChangedEvent e)
            => await ExecuteAsync(ErrorCodeBase + 3, async () => {
                await _marketDataDenormalizer.UpdateFuturesOptionContractAsync(e);
                await WriteConsoleAsync($"FuturesOptionContractChanged: {e.Contract.ContractId}");
            });

        /// <summary>
        /// execute yield curve rate added event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(YieldCurveRateAddedEvent e)
            => await ExecuteAsync(ErrorCodeBase + 3, async () => {
                await _marketDataDenormalizer.InsertYieldCurveRateAsync(e);
                await WriteConsoleAsync($"YieldCurveRateAdded: {e.YieldCurveRate.ValueDate.ToString("yyyy-MM-dd")}");
            });

        /// <summary>
        /// execute yield curve rate changed event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(YieldCurveRateChangedEvent e)
            => await ExecuteAsync(ErrorCodeBase + 3, async () => {
                await _marketDataDenormalizer.UpdateYieldCurveRateAsync(e);
                await WriteConsoleAsync($"YieldCurveRateChanged: {e.YieldCurveRate.ValueDate.ToString("yyyy-MM-dd")}");
            });

        /// <summary>
        /// execute yield curve removed event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(YieldCurveRateRemovedEvent e)
             => await ExecuteAsync(ErrorCodeBase + 3, async () => {
                 await _marketDataDenormalizer.DeleteYieldCurveRateAsync(e);
                 await WriteConsoleAsync($"YieldCurveRateRemoved: {e.ValueDate.ToString("yyyy-MM-dd")}");
             });

        /// <summary>
        /// execute yield curve imported rates event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(YieldCurveRatesImportedEvent e)
            => await ExecuteAsync(ErrorCodeBase + 3, async () => {
                await _marketDataDenormalizer.InsertYieldCurveRatesAsync(e);
                var yieldCurveRates = e.YieldCurveRates.ToList();
                var minDate = yieldCurveRates.Min(o => o.ValueDate).ToString("yyyy-MM-dd");
                var maxDate = yieldCurveRates.Max(o => o.ValueDate).ToString("yyyy-MM-dd");
                await WriteConsoleAsync($"YieldCurveRatesImported: {yieldCurveRates.Count} rates from: {minDate} to: {maxDate}");
            });

        protected override async Task WriteConsoleAsync(string message) => await WriteConsoleAsync(StatusSourceType.MarketData, message);

        protected override async Task WriteConsoleAsync(Exception ex, int errorCode) => await WriteConsoleAsync(StatusSourceType.MarketData, ex, errorCode);
    }
}
