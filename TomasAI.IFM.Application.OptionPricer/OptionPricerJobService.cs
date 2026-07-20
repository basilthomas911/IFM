using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.OptionPricer
{
    public class OptionPricerJobService : IOptionPricerJobService
    {
        readonly IOptionPricerServiceApi _optionPricerServiceApi;
        readonly IStatusConsoleWriter _statusConsoleWriter;
        readonly IOptionPricerCommandApi _optionPricerCommand;

        public OptionPricerJobService(
            IOptionPricerServiceApi optionPricerServiceApi,
            IStatusConsoleWriter statusConsoleWriter,
            IOptionPricerCommandApi optionPricerCommand)
        {
            _optionPricerServiceApi = optionPricerServiceApi ?? throw new ArgumentNullException(nameof(optionPricerServiceApi));    
            _statusConsoleWriter = statusConsoleWriter ?? throw new ArgumentNullException(nameof(statusConsoleWriter));
            _optionPricerCommand = optionPricerCommand ?? throw new ArgumentNullException(nameof(optionPricerCommand));
        }

        /// <summary>
        /// run option pricing job 
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task RunOptionPricerJobAsync(SpreadDistributionJobSubmittedEvent e)
        {
            ServiceResult<SpreadDistributionJobReadModel> serviceResult;
            try
            {
                serviceResult = await _optionPricerServiceApi.ExecuteAsync(e.SpreadDistributionJob);
            }
            catch (Exception ex)
            {
                serviceResult = new ServiceFailed<SpreadDistributionJobReadModel>(-102, ex.GetErrorMessage());
            }
            if (serviceResult.Success && serviceResult.Value is not null)
            {
                var spreadJob = serviceResult.Value;
                await _optionPricerCommand.CompleteSpreadDistributionJobAsync(spreadJob.EntityId, DateTime.Now, SpreadDistributionJobStatus.Completed);
                await WriteConsoleAsync($"OptionPricerJobCompleted: {spreadJob.JobSubmitted:hh:mm:ss} Duration {spreadJob.Duration:F4} seconds");
            }
            else
            {
                await _optionPricerCommand.FailSpreadDistributionJobAsync(e.SpreadDistributionJob.EntityId, DateTime.Now, SpreadDistributionJobStatus.Failed, serviceResult.ErrorMessage);
                await WriteConsoleAsync(serviceResult.ErrorCode, serviceResult.ErrorMessage);
            }
        }

        private async Task WriteConsoleAsync(string message) => await _statusConsoleWriter.WriteConsoleAsync(LogSourceType.OptionPricer, message);

        private async Task WriteConsoleAsync(int statusCode, string message) => await _statusConsoleWriter.WriteConsoleAsync(LogSourceType.OptionPricer, statusCode, message);

    }
}
