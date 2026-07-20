using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.CommandParameters;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// create option pricer command api
/// </summary>
/// <param name="commandSvc"></param>
/// <exception cref="ArgumentNullException"></exception>
public class OptionPricerCommandApi(ICommandServiceApi commandSvc) : IOptionPricerCommandApi
{
    readonly ICommandServiceApi _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// insert spread distribution 
    /// </summary>
    /// <param name="putSpreadDistribution"></param>
    /// <param name="callSpreadDistribution"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertSpreadDistributionsAsync(
        SpreadDistributionReadModel putSpreadDistribution, 
        SpreadDistributionReadModel callSpreadDistribution)
        => await new InsertSpreadDistributionParameter(IsArgumentNull.Set(putSpreadDistribution), IsArgumentNull.Set(callSpreadDistribution), InsertSpreadDistributionCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(OptionPricerUriPath.InsertSpreadDistribution, e));

    /// <summary>
    /// Deletes the specified spread distribution asynchronously.
    /// </summary>
    /// <remarks>Ensure that the provided entity ID corresponds to an existing spread distribution. The
    /// operation is performed asynchronously and may fail if the specified distribution does not exist.</remarks>
    /// <param name="e">The identifier of the spread distribution to delete. Must contain a valid trade ID and value date.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a ServiceResult with the unique
    /// identifier of the deleted spread distribution.</returns>
    public async Task<ServiceResult<Guid>> DeleteSpreadDistributionAsync(SpreadDistributionEntityId e, TradeStatus tradeStatus, int daysToExpiry)
        => await new DeleteSpreadDistributionParameter(e.TradeId, e.ValueDate, tradeStatus, daysToExpiry, DeleteSpreadDistributionCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(OptionPricerUriPath.DeleteSpreadDistribution, e));

    /// <summary>
    /// submit spread distribution job
    /// </summary>
    /// <param name="spreadDistributionJob"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> SubmitSpreadDistributionJobAsync(SpreadDistributionJobReadModel spreadDistributionJob)
        => await new SubmitSpreadDistributionJobParameter(IsArgumentNull.Set(spreadDistributionJob), SubmitSpreadDistributionJobCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(OptionPricerUriPath.SubmitSpreadDistributionJob, e));

    /// <summary>
    /// complete spread distribution job
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="jobCompleted"></param>
    /// <param name="jobStatus"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CompleteSpreadDistributionJobAsync(
        SpreadDistributionJobEntityId entityId, 
        DateTime jobCompleted, 
        SpreadDistributionJobStatus jobStatus)
        => await new CompleteSpreadDistributionJobParameter(entityId, jobCompleted, jobStatus, CompleteSpreadDistributionJobCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(OptionPricerUriPath.CompleteSpreadDistributionJob, e));

    /// <summary>
    /// fail spread distribution job
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="jobFailed"></param>
    /// <param name="jobStatus"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> FailSpreadDistributionJobAsync(
        SpreadDistributionJobEntityId entityId, 
        DateTime jobFailed, 
        SpreadDistributionJobStatus jobStatus, 
        string errorMessage)
        => await new FailSpreadDistributionJobParameter(entityId, jobFailed, jobStatus, errorMessage, FailSpreadDistributionJobCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(OptionPricerUriPath.FailSpreadDistributionJob, e));

    /// <summary>
    /// clear spread distribution jobs
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ClearSpreadDistributionJobAsync(SpreadDistributionJobEntityId entityId)
        => await new ClearSpreadDistributionJobParameter(entityId, ClearSpreadDistributionJobCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(OptionPricerUriPath.ClearSpreadDistributionJob, e));

    /// <summary>
    /// delete spread distribution jobs in progress
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> DeleteSpreadDistributionJobsInProgressAsync(SpreadDistributionJobEntityId entityId)
        => await new DeleteSpreadDistributionJobsInProgressParameter(entityId, DeleteSpreadDistributionJobsInProgressCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(OptionPricerUriPath.DeleteSpreadDistributionJobsInProgress, e));
}
