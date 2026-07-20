using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Exceptions;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command;

public static class CompleteSpreadDistributionJob
{
    /// <summary>
    /// Executes the <see cref="CompleteSpreadDistributionJobCommand"/> against the provided <see cref="SpreadDistributionJobCommandState"/>.
    /// </summary>
    /// <param name="e">The command to execute.</param>
    /// <param name="state">The state against which to execute the command.</param>
    /// <returns></returns>
    /// <exception cref="SpreadDistributionJobNotInProgressException"></exception>
    public static bool Execute(this CompleteSpreadDistributionJobCommand e, SpreadDistributionJobCommandState state)
       => e switch
       {
           _ when !state.IsJobStatusInProgress
               => throw new SpreadDistributionJobNotInProgressException(e.EntityId),
           _ => state.Update(e.CreateSpreadDistributionJobCompletedEvent(), e)
       };

    /// <summary>
    /// Creates a <see cref="SpreadDistributionJobStatusUpdatedEvent"/> representing a successful job
    /// completion from a <see cref="CompleteSpreadDistributionJobCommand"/>.
    /// Sets <c>JobStatus</c> to <see cref="SpreadDistributionJobStatus.Completed"/>.
    /// </summary>
    /// <param name="e">The command signalling that the spread distribution job has completed successfully.</param>
    /// <returns>A fully populated <see cref="SpreadDistributionJobStatusUpdatedEvent"/> with status <c>Completed</c>.</returns>
    internal static SpreadDistributionJobStatusUpdatedEvent CreateSpreadDistributionJobCompletedEvent(this CompleteSpreadDistributionJobCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobStatusUpdatedEvent.Actor, SpreadDistributionJobStatusUpdatedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            JobStatus = SpreadDistributionJobStatus.Completed
        };


}
