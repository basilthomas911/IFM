using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event.Extensions;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Services;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Services.Contracts;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event;

public static class SpreadDistributionJobSubmitted
{
    static SpreadDistributionJobSubmitted()
    {
        ServiceId = LogSourceType.SpreadDistributionJobEvent.ToStringFast();
    }
    static string ServiceId { get; } = default!;

    /// <summary>
    /// Executes the logic for handling a spread distribution job submission event asynchronously and writes status
    /// </summary>
    /// <param name="e">The submitted job event containing the spread distribution job details.</param>
    /// <param name="context">The event actor context used to dispatch queries and commands.</param>
    /// <param name="statusConsoleWriter">The console writer for logging status updates.</param>
    /// <param name="logger">The logger for recording errors and other events.</param>
    /// <returns><see langword="true"/> if the handler executed without throwing; otherwise <see langword="false"/>.</returns>
    public static async ValueTask<bool> ExecuteAsync(
        this SpreadDistributionJobSubmittedEvent e, IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger logger)
    {
        var source = $"SpreadDistributionJobSubmittedEvent for Job: {e.EntityId.Format()}";
        try
        {
            var jobService = e.GetSpreadDistributionJobService(context);
            var serviceResult = await jobService.ExecuteAsync();
            if (serviceResult.Success && serviceResult.Value is not null)
            {
                var spreadJob = serviceResult.Value;
                await context.CompleteSpreadDistributionJobAsync(spreadJob.EntityId, DateTime.Now, SpreadDistributionJobStatus.Completed);
                await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesItiSignalEvent, $"SpreadDistributionJobCompleted: {spreadJob.JobSubmitted:hh:mm:ss} Duration {spreadJob.Duration:F4} seconds");
            }
            else
            {
                await context.FailSpreadDistributionJobAsync(e.SpreadDistributionJob.EntityId, DateTime.Now, SpreadDistributionJobStatus.Failed, serviceResult.ErrorMessage);
                await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesItiSignalEvent, serviceResult.ErrorCode, serviceResult.ErrorMessage);
            }
            return true;
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesItiSignalEvent, FuturesEodDataInsertedCompleteEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}:  ExecuteAsync handler failed", source);
        }
        return false;
    }

    /// <summary>
    /// Resolves the appropriate <see cref="ISpreadDistributionJobService"/> implementation for the
    /// trade type carried by a <see cref="SpreadDistributionJobSubmittedEvent"/>.
    /// The service is responsible for executing the spread distribution calculation and returning
    /// an updated <see cref="SpreadDistributionJobReadModel"/>.
    /// </summary>
    /// <param name="e">The submitted event whose <c>SpreadDistributionJob.TradeType</c> determines the service.</param>
    /// <param name="state">The current event-sourced state of the spread distribution job actor.</param>
    /// <param name="context">The actor context providing infrastructure dependencies for the service.</param>
    /// <returns>
    /// A concrete <see cref="ISpreadDistributionJobService"/> matched to the trade type
    /// (e.g. <c>IronCondorSpreadDistributionJobService</c> for <see cref="TradeType.LongIronCondor"/>
    /// and <see cref="TradeType.ShortIronCondor"/>).
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the trade type on the event is not handled by any registered service.
    /// </exception>
    internal static ISpreadDistributionJobService GetSpreadDistributionJobService(this SpreadDistributionJobSubmittedEvent e, IEventActorContext context)
           => e.SpreadDistributionJob.TradeType switch
           {
               TradeType.LongIronCondor => new IronCondorSpreadDistributionJobService(e, context),
               TradeType.ShortIronCondor => new IronCondorSpreadDistributionJobService(e, context),
               _ => throw new NotSupportedException($"Unsupported trade type: {e.SpreadDistributionJob.TradeType}")
           };
}
