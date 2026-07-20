using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Exceptions;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command;

public static class ClearSpreadDistributionJob
{
    /// <summary>
    /// Executes the <see cref="ClearSpreadDistributionJobCommand"/> against the provided <see cref="SpreadDistributionJobCommandState"/>.
    /// </summary>
    /// <param name="e">The clear spread distribution job command to execute.</param>
    /// <param name="state">The current state of the spread distribution job, used to verify the job's status and apply updates.</param>
    /// <returns>true if the job status was successfully updated; otherwise, an exception is thrown.</returns>
    /// <exception cref="SpreadDistributionJobNotInProgressException">Thrown if the job is not in progress for the specified entity ID.</exception>
    public static bool Execute(this ClearSpreadDistributionJobCommand e, SpreadDistributionJobCommandState state)
        => e switch
        {
            _ when !state.IsJobStatusInProgress
                => throw new SpreadDistributionJobNotInProgressException(e.EntityId),
            _ => state.Update(e.CreateSpreadDistributionJobClearedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="SpreadDistributionJobStatusUpdatedEvent"/> indicating that the spread distribution job has been cleared.
    /// </summary>
    /// <param name="e">The clear spread distribution job command.</param>
    /// <returns>A fully populated <see cref="SpreadDistributionJobStatusUpdatedEvent"/> with status <c>Cleared</c>.</returns>
    internal static SpreadDistributionJobStatusUpdatedEvent CreateSpreadDistributionJobClearedEvent(this ClearSpreadDistributionJobCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobStatusUpdatedEvent.Actor, SpreadDistributionJobStatusUpdatedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            JobStatus = SpreadDistributionJobStatus.Cleared
        };
}
