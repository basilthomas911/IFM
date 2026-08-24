using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event.Extensions;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Services;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Services.Contracts;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event;

/// <summary>Provides the SpreadDistributionJobSubmitted implementation.</summary>
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
        this SpreadDistributionJobSubmittedEvent e,
        IEventActorContext context,
        IEventActorContext optionPricerCommandApi,
        IEventActorContext tradeCommandApi,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {
        var jobService = e.GetSpreadDistributionJobService(context, tradeCommandApi);
        var serviceResult = await jobService.ExecuteAsync().ConfigureAwait(false);
        if (serviceResult.Success && serviceResult.Value is not null)
        {
            var spreadJob = serviceResult.Value;
            await OptionPricerCommandApiExtensions.CompleteSpreadDistributionJobAsync(optionPricerCommandApi, spreadJob.EntityId, DateTime.UtcNow, SpreadDistributionJobStatus.Completed).ConfigureAwait(false);
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.SpreadDistributionJobEvent, $"SpreadDistributionJobCompleted: {spreadJob.JobSubmitted:HH:mm:ss} Duration {spreadJob.Duration:F4} ms").ConfigureAwait(false);
        }
        else
        {
            await OptionPricerCommandApiExtensions.FailSpreadDistributionJobAsync(optionPricerCommandApi, e.SpreadDistributionJob.EntityId, DateTime.UtcNow, SpreadDistributionJobStatus.Failed, serviceResult.ErrorMessage).ConfigureAwait(false);
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.SpreadDistributionJobEvent, serviceResult.ErrorCode, serviceResult.ErrorMessage).ConfigureAwait(false);
        }
        return true;
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
    internal static ISpreadDistributionJobService GetSpreadDistributionJobService(
        this SpreadDistributionJobSubmittedEvent e,
        IEventActorContext context,
        IEventActorContext tradeCommandApi)
           => e.SpreadDistributionJob.TradeType switch
           {
               TradeType.LongIronCondor => new IronCondorSpreadDistributionJobService(e, context, tradeCommandApi),
               TradeType.ShortIronCondor => new IronCondorSpreadDistributionJobService(e, context, tradeCommandApi),
               _ => throw new NotSupportedException($"Unsupported trade type: {e.SpreadDistributionJob.TradeType}")
           };
}
