using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models
{
    public class SpreadDistributionJobModel : BaseModel<SpreadDistributionJobModel>
    {
        readonly IOptionPricerCommandApi _optionPricerCommandApi;
        readonly IOptionPricerQueryApi _optionPricerQueryApi;
        //readonly ISpreadDistributionJobUIEventConsumer _spreadDistributionJobEventConsumer;

        /// <summary>
        /// create spread distribution job controller
        /// </summary>
        /// <param name="appRoot"></param>
        public SpreadDistributionJobModel(
            IOptionPricerCommandApi optionPricerCommandApi,
            IOptionPricerQueryApi optionPricerQueryApi)
            //ISpreadDistributionJobUIEventConsumer spreadDistributionJobEventConsumer)
        {
            _optionPricerCommandApi = optionPricerCommandApi ?? throw new ArgumentNullException(nameof(optionPricerCommandApi));
            _optionPricerQueryApi = optionPricerQueryApi ?? throw new ArgumentNullException(nameof(optionPricerQueryApi));
            //_spreadDistributionJobEventConsumer = spreadDistributionJobEventConsumer ?? throw new ArgumentNullException(nameof(spreadDistributionJobEventConsumer));
        }

        /// <summary>
        /// submit spread distribution job to option pricer service
        /// </summary>
        /// <param name="spreadDistributionJob"></param>
        public async Task SubmitSpreadDistributionJobAsync(SpreadDistributionJobReadModel spreadDistributionJob) 
            => await ExecuteCommandAsync(() => _optionPricerCommandApi.SubmitSpreadDistributionJobAsync(spreadDistributionJob));

        /// <summary>
        /// clear spread distribution jobs
        /// </summary>
        /// <param name="entityId"></param>
        public async Task ClearSpreadDistributionJobsAsync(SpreadDistributionJobEntityId entityId)
            => await ExecuteCommandAsync(() => _optionPricerCommandApi.ClearSpreadDistributionJobAsync(entityId));

        /// <summary>
        /// delete spread distribution jobs in progress
        /// </summary>
        /// <param name="entityId"></param>
        public async Task DeleteSpreadDistributionJobsInProgressAsync(SpreadDistributionJobEntityId entityId)
            => await ExecuteCommandAsync(() => _optionPricerCommandApi.DeleteSpreadDistributionJobsInProgressAsync(entityId));

        /// <summary>
        /// is spread dustribution job in progress
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="isJobInProgress"></param>
        public async Task IsSpreadDistributionJobInProgressAsync(int orderId, int tradeId, Action<bool> isJobInProgress)
            => await ExecuteAsync(() => _optionPricerQueryApi.IsSpreadDistributionJobInProgressAsync(orderId, tradeId), e => isJobInProgress(e.Value));

        /// <summary>
        /// start spread distribution job consumer
        /// </summary>
        public async Task StartSpreadDistributionJobConsumerAsync() 
            //=> await ExecuteAsync( _spreadDistributionJobEventConsumer.StartAsync );
            => await Task.CompletedTask;

        /// <summary>
        /// stop spread distribution job consumer
        /// </summary>
        public async Task StopSpreadDistributionJobConsumerAsync()
            //=> await ExecuteAsync( _spreadDistributionJobEventConsumer.StopAsync );
            => await Task.CompletedTask;

    }
}
