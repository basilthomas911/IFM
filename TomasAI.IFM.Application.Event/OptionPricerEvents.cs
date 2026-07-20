using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;

namespace TomasAI.IFM.Application.Event
{
    public class OptionPricerEvents : BaseEvents,
        IAsyncEventHandler<SpreadDistributionInsertedEvent>,
        IAsyncEventHandler<SpreadDistributionJobCreatedEvent>,
        IAsyncEventHandler<SpreadDistributionJobCompletedEvent>,
        IAsyncEventHandler<SpreadDistributionJobFailedEvent>
    {
        private const int ErrorCodeBase = 4000;
        private readonly IOptionPricerEventDenormalizerApi _optionPricerDenormalizer;

        public OptionPricerEvents(IOptionPricerEventDenormalizerApi optionPricerDenormalizer, IStatusConsoleServiceApi statusConsoleLog)
            :base(statusConsoleLog)
        {
            _optionPricerDenormalizer = optionPricerDenormalizer;
        }

        /// <summary>
        /// save spread distribution to storage
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(SpreadDistributionInsertedEvent e) => await ExecuteAsync(ErrorCodeBase + 1, () => _optionPricerDenormalizer.InsertSpreadDistributionAsync(e));

        /// <summary>
        /// create spread distribution job
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(SpreadDistributionJobCreatedEvent e) => await ExecuteAsync(ErrorCodeBase + 2, () => _optionPricerDenormalizer.InsertSpreadDistributionJobAsync(e));

        /// <summary>
        /// update spread distribution job status to completed
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(SpreadDistributionJobCompletedEvent e) => await ExecuteAsync(ErrorCodeBase + 3, () => _optionPricerDenormalizer.UpdateSpreadDistributionJobCompletedAsync(e));

        /// <summary>
        /// update spread distribution job status to failed
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(SpreadDistributionJobFailedEvent e) => await ExecuteAsync(ErrorCodeBase + 4, () => _optionPricerDenormalizer.UpdateSpreadDistributionJobFailedAsync(e));


        protected override async Task WriteConsoleAsync(string message) => await WriteConsoleAsync(StatusSourceType.OptionPricer, message);

        protected override async Task WriteConsoleAsync(Exception ex, int errorCode) => await WriteConsoleAsync(StatusSourceType.OptionPricer, ex, errorCode);

    }
}
