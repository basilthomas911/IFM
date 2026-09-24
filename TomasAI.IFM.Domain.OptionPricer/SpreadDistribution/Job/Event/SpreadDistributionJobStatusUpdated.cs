using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event;

/// <summary>Handles the SpreadDistributionJobStatusUpdatedEvent message in the SpreadDistributionJobEventActor lifecycle.</summary>
public static class SpreadDistributionJobStatusUpdated
{
    static SpreadDistributionJobStatusUpdated()
    {
        ServiceId = LogSourceType.SpreadDistributionJobEvent.ToStringFast();
    }
    static string ServiceId { get; } = default!;

    /// <summary>Applies a spread-distribution job status update and reports the outcome.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this SpreadDistributionJobStatusUpdatedEvent e, IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger<SpreadDistributionJobEventActor> logger)
    {
        await statusConsoleWriter.WriteConsoleAsync(
            LogSourceType.SpreadDistributionJobEvent,
            $"SpreadDistributionJobStatusUpdated: Status: {e.JobStatus}").ConfigureAwait(false);
        return true;
    }
}
