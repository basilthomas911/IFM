using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Exceptions;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command;

public static class FailSpreadDistributionJob
{
    /// <summary>
    /// Executes the <see cref="FailSpreadDistributionJobCommand"/> against the provided <see cref="SpreadDistributionJobCommandState"/>.
    /// </summary>
    /// <param name="e">The fail spread distribution job command to execute.</param>
    /// <param name="state">The current state of the spread distribution job, used to verify the job's status and apply updates.</param>
    /// <returns>true if the job status was successfully updated; otherwise, an exception is thrown.</returns>
    /// <exception cref="SpreadDistributionJobNotInProgressException">Thrown if the job is not in progress for the specified entity ID.</exception>
    public static bool Execute(this FailSpreadDistributionJobCommand e, SpreadDistributionJobCommandState state)
        => e switch
        {
            _ when !state.IsJobStatusInProgress
                => throw new SpreadDistributionJobNotInProgressException(e.EntityId),
            _ => state.Update(e.CreateSpreadDistributionJobFailedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="SpreadDistributionJobStatusUpdatedEvent"/> representing a job failure
    /// from a <see cref="FailSpreadDistributionJobCommand"/>.
    /// Sets <c>JobStatus</c> to <see cref="SpreadDistributionJobStatus.Failed"/>.
    /// </summary>
    /// <param name="e">The command signalling that the spread distribution job has failed.</param>
    /// <returns>A fully populated <see cref="SpreadDistributionJobStatusUpdatedEvent"/> with status <c>Failed</c>.</returns>
    internal static SpreadDistributionJobStatusUpdatedEvent CreateSpreadDistributionJobFailedEvent(this FailSpreadDistributionJobCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobStatusUpdatedEvent.Actor, SpreadDistributionJobStatusUpdatedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            JobStatus = SpreadDistributionJobStatus.Failed
        };

}
