using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Event.Denormalizer
{
    public class OptionPricerEventDenormalizer : BaseEventDenormalizer,
        IAsyncEventHandler<SpreadDistributionInsertedEvent>,
        IAsyncEventHandler<SpreadDistributionJobCreatedEvent>,
        IAsyncEventHandler<SpreadDistributionJobSucceededEvent>,
        IAsyncEventHandler<SpreadDistributionJobFaultedEvent>
    {
        private const int Err_OptionPricerEventDenormalizer = 5004;
        private readonly IOptionPricerDbContext _dbOptionPricer;
        private readonly IOptionPricerEventProducer _eventProducer;

        public OptionPricerEventDenormalizer(IDbContextFactory dbFactory, IOptionPricerEventProducer eventProducer, ILogger<OptionPricerEventDenormalizer> logger):base(logger)
        {
            _dbOptionPricer = dbFactory.OptionPricerDb as IOptionPricerDbContext;
            _eventProducer = eventProducer;
            SetEventProducer(e => _eventProducer.PostEventAsync((dynamic)e));
        }

        /// <summary>
        /// insert spread distribution into query store
        /// </summary>
        /// <param name="e">SpreadDistributionInsertedEvent</param>
        /// <returns></returns>
        public async Task ExecuteAsync(SpreadDistributionInsertedEvent e)
            => await DenormalizeAsync(e, Err_OptionPricerEventDenormalizer, () => _dbOptionPricer.DbWriter.InsertSpreadDistributionAsync(e.SpreadDistribution));
        
        /// <summary>
        /// insert spread distribution job into query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(SpreadDistributionJobCreatedEvent e)
            => await DenormalizeAsync(e, Err_OptionPricerEventDenormalizer, () => _dbOptionPricer.DbWriter.InsertSpreadDistributionJobAsync(e.SpreadDistributionJob));

        /// <summary>
        /// update spread distribution job status to completed
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(SpreadDistributionJobSucceededEvent e)
            => await DenormalizeAsync(e, Err_OptionPricerEventDenormalizer, () => _dbOptionPricer.DbWriter.UpdateSpreadDistributionJobCompletedAsync(
                   jobId: e.JobId,
                   jobCompleted: e.JobCompleted,
                   jobStatus: e.JobStatus));

        /// <summary>
        /// update spread distribution job status to failed
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(SpreadDistributionJobFaultedEvent e)
            => await DenormalizeAsync(e, Err_OptionPricerEventDenormalizer, () => _dbOptionPricer.DbWriter.UpdateSpreadDistributionJobFailedAsync(
                   jobId: e.JobId,
                   jobFailed: e.JobFailed,
                   jobStatus: e.JobStatus));

    }
}
