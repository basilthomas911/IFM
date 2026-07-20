using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.State;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command;

public static class DeleteSpreadDistribution
{
    /// <summary>
    /// Handle an <see cref="DeleteSpreadDistributionCommand"/> by building the corresponding
    /// <see cref="SpreadDistributionDeletedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this DeleteSpreadDistributionCommand e, SpreadDistributionCommandState state)
        => state.Update(e.CreateSpreadDistributionDeletedEvent(), e);

    /// <summary>
    /// Creates a new SpreadDistributionDeletedEvent instance using the details provided in the specified command.
    /// </summary>
    /// <param name="e">The command containing the entity identifier and metadata required to construct the event.</param>
    /// <returns>A SpreadDistributionDeletedEvent that encapsulates the deleted spread distribution metadata.</returns>
    internal static SpreadDistributionDeletedEvent CreateSpreadDistributionDeletedEvent(this DeleteSpreadDistributionCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionDeletedEvent.Actor, SpreadDistributionDeletedEvent.Verb, e.EntityId.Format()),
            EntityId = new SpreadDistributionEntityId(e.EntityId.TradeId, e.EntityId.ValueDate),
            DeletedOn = e.OriginatedOn,
            DeletedBy = e.OriginatedBy
        };
}
