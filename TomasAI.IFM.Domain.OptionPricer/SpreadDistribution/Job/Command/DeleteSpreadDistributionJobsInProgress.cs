using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Exceptions;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command;

public static class DeleteSpreadDistributionJobsInProgress
{
    /// <summary>
    /// Executes the DeleteSpreadDistributionJobsInProgressCommand against the provided SpreadDistributionJobCommandState.
    /// </summary>
    /// <param name="e">The command requesting deletion of all in-progress jobs for the associated option trade.</param>
    /// <param name="state">The state object tracking the status of spread distribution jobs.</param>
    /// <returns>true if the deletion is successfully executed; otherwise, an exception is thrown.</returns>
    /// <exception cref="SpreadDistributionJobNotInProgressException">Thrown when the specified job is not in progress for the given entity identifier.</exception>
    public static bool Execute(this DeleteSpreadDistributionJobsInProgressCommand e, SpreadDistributionJobCommandState state)
        => e switch
        {
            _ when !state.IsJobStatusInProgress
                => throw new SpreadDistributionJobNotInProgressException(e.EntityId),
            _ => state.Update(e.CreateSpreadDistributionJobsInProgressDeletedEvent(), e)
        };

    /// <summary>
    /// Creates a SpreadDistributionJobsInProgressDeletedEvent from a DeleteSpreadDistributionJobsInProgressCommand.
    /// </summary>
    /// <param name="e">The command requesting deletion of all in-progress jobs for the associated option trade.</param>
    /// <returns>A fully populated <see cref="SpreadDistributionJobsInProgressDeletedEvent"/>.</returns>
    internal static SpreadDistributionJobsInProgressDeletedEvent CreateSpreadDistributionJobsInProgressDeletedEvent(this DeleteSpreadDistributionJobsInProgressCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobsInProgressDeletedEvent.Actor, SpreadDistributionJobsInProgressDeletedEvent.Verb, e.EntityId.Format()),
            EntityId = new OptionTradeEntityId(e.EntityId.OrderId, e.EntityId.TradeId),
            CreatedBy = e.OriginatedBy,
            CreatedOn = e.OriginatedOn,
        };

}
