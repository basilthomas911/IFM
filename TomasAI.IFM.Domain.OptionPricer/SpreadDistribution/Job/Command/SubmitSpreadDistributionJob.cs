using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command;

public static class SubmitSpreadDistributionJob
{
    /// <summary>
    /// Executes the <see cref="SubmitSpreadDistributionJobCommand"/> against the current <see cref="SpreadDistributionJobCommandState"/>.
    /// </summary>
    /// <param name="e">The submit spread distribution job command to execute.</param>
    /// <param name="state">The current state of the spread distribution job.</param>
    /// <returns>true if the job was successfully submitted; otherwise, an exception is thrown.</returns>
    public static bool Execute(this SubmitSpreadDistributionJobCommand e, SpreadDistributionJobCommandState state)
       => e switch
       {
           _ when !state.IsJobStatusInProgress
               => state.Update(e.CreateSpreadDistributionJobSubmittedEvent(), e),
           _ => state.Update(e.CreateSpreadDistributionJobInProgressEvent(state.SpreadDistributionJob!), e)
       };

    /// <summary>
    /// Creates a <see cref="SpreadDistributionJobSubmittedEvent"/> from a
    /// <see cref="SubmitSpreadDistributionJobCommand"/>.
    /// Embeds the full <see cref="SpreadDistributionJobReadModel"/> payload from the command into the event.
    /// </summary>
    /// <param name="e">The command carrying the spread distribution job to submit.</param>
    /// <returns>A fully populated <see cref="SpreadDistributionJobSubmittedEvent"/>.</returns>
    internal static SpreadDistributionJobSubmittedEvent CreateSpreadDistributionJobSubmittedEvent(this SubmitSpreadDistributionJobCommand e)
         => new()
         {
             CommandId = e.CommandId,
             Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobSubmittedEvent.Actor, SpreadDistributionJobSubmittedEvent.Verb, e.EntityId.Format()),
             EntityId = e.EntityId,
             SpreadDistributionJob = e.SpreadDistributionJob,
             CreatedBy = e.OriginatedBy,
             CreatedOn = e.OriginatedOn
         };

    /// <summary>
    /// Creates a <see cref="SpreadDistributionJobInProgressEvent"/> from a
    /// <see cref="SubmitSpreadDistributionJobCommand"/> when a job is already running for the entity.
    /// Uses the current <paramref name="spreadDistributionJob"/> read model rather than the one embedded
    /// in the command, preserving the active job state.
    /// </summary>
    /// <param name="e">The submit command that triggered the in-progress transition.</param>
    /// <param name="spreadDistributionJob">The current read model of the job already in progress.</param>
    /// <returns>A fully populated <see cref="SpreadDistributionJobInProgressEvent"/>.</returns>
    internal static SpreadDistributionJobInProgressEvent CreateSpreadDistributionJobInProgressEvent(this SubmitSpreadDistributionJobCommand e, SpreadDistributionJobReadModel spreadDistributionJob)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobInProgressEvent.Actor, SpreadDistributionJobInProgressEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            SpreadDistributionJob = spreadDistributionJob,
            CreatedBy = e.OriginatedBy,
            CreatedOn = e.OriginatedOn
        };
}
