using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Command.Client;

/// <summary>
/// create option pricer command api
/// </summary>
/// <param name="commandSvc"></param>
/// <exception cref="ArgumentNullException"></exception>
public class OptionPricerCommandApi(ICommandService commandSvc) : IOptionPricerCommandApi
{
    const string OptionPricerController = "OptionPricer";
    readonly ICommandService _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// insert spread distribution 
    /// /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertSpreadDistributionsAsync(
        SpreadDistributionReadModel putSpreadDistribution, 
        SpreadDistributionReadModel callSpreadDistribution)
        => await new InsertSpreadDistributionCommand( putSpreadDistribution, callSpreadDistribution)
            .ExecuteAsync(e =>_commandSvc.PostApiCommandAsync(e, OptionPricerController));

    /// <summary>
    /// submit spread distribution job
    /// </summary>
    /// <param name="spreadDistributionJob"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> SubmitSpreadDistributionJobAsync(SpreadDistributionJobReadModel spreadDistributionJob)
        => await new SubmitSpreadDistributionJobCommand(spreadDistributionJob)
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, OptionPricerController));


    /// <summary>
    /// complete spreaddistribution job
    /// </summary>
    /// <param name="optionTradeId"></param>
    /// <param name="jobId"></param>
    /// <param name="jobCompleted"></param>
    /// <param name="jobStatus"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CompleteSpreadDistributionJobAsync(
        OptionTradeEntityId optionTradeId, 
        int jobId, DateTime jobCompleted, 
        SpreadDistributionJobStatus jobStatus)
        => await new CompleteSpreadDistributionJobCommand( optionTradeId, jobId, jobCompleted,  jobStatus)
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, OptionPricerController));

    /// <summary>
    /// fail spread distribution job
    /// </summary>
    /// <param name="optionTradeId"></param>
    /// <param name="jobId"></param>
    /// <param name="jobFailed"></param>
    /// <param name="jobStatus"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> FailSpreadDistributionJobAsync(
        OptionTradeEntityId optionTradeId, 
        int jobId, 
        DateTime jobFailed, 
        SpreadDistributionJobStatus jobStatus, 
        string errorMessage)
        => await new FailSpreadDistributionJobCommand(optionTradeId, jobId, jobFailed, jobStatus, errorMessage)
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, OptionPricerController));

    /// <summary>
    /// clear spread distribution jobs
    /// </summary>
    /// <param name="optionTradeId"></param>
    /// <returns></returns>
    public async  Task<ServiceResult<Guid>> ClearSpreadDistributionJobAsync(OptionTradeEntityId optionTradeId)
         => await new ClearSpreadDistributionJobCommand(optionTradeId)
         .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, OptionPricerController));

    /// <summary>
    /// delete spread distribution jobs in progress
    /// </summary>
    /// <param name="optionTradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> DeleteSpreadDistributionJobsInProgressAsync(OptionTradeEntityId optionTradeId)
         => await new DeleteSpreadDistributionJobsInProgressCommand (optionTradeId)
         .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, OptionPricerController));
}
