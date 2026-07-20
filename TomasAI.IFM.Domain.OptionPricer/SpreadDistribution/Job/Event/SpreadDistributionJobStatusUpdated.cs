using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event;

public static class SpreadDistributionJobStatusUpdated
{
    static SpreadDistributionJobStatusUpdated()
    {
        ServiceId = LogSourceType.SpreadDistributionJobEvent.ToStringFast();
    }
    static string ServiceId { get; } = default!;

    public static async ValueTask<bool> ExecuteAsync(
        this SpreadDistributionJobStatusUpdatedEvent e, IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger logger)
    {
        var source = $"SpreadDistributionJobStatusUpdatedEvent for Job: {e.EntityId.Format()}";
        try
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesItiSignalEvent, $"SpreadDistributionJobStatusUpdated:  Status: {e.JobStatus}");
            return true;
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesItiSignalEvent, FuturesEodDataInsertedCompleteEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}:  ExecuteAsync handler failed", source);
        }
        return false;
    }
}
