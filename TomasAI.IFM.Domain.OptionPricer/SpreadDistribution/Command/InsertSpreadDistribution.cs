using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.State;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command;

public static class InsertSpreadDistribution
{
    /// <summary>
    /// Handle an <see cref="InsertSpreadDistributionCommand"/> by building the corresponding
    /// <see cref="SpreadDistributionInsertedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this InsertSpreadDistributionCommand e, SpreadDistributionCommandState state)
        => state.Update(e.CreateSpreadDistributionInsertedEvent(), e);

    /// <summary>
    /// Creates a new SpreadDistributionInsertedEvent instance using the details provided in the specified command.
    /// </summary>
    /// <param name="e">The command containing the entity identifier, spread distribution values, and metadata required to construct the
    /// event. Cannot be null.</param>
    /// <returns>A SpreadDistributionInsertedEvent that encapsulates the inserted spread distribution and associated metadata.</returns>
    internal static SpreadDistributionInsertedEvent CreateSpreadDistributionInsertedEvent(this InsertSpreadDistributionCommand e)
        => new ()
        {
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionInsertedEvent.Actor, SpreadDistributionInsertedEvent.Verb, e.EntityId.Format()),
            EntityId = new SpreadDistributionEntityId(e.EntityId.TradeId, e.EntityId.ValueDate),
            PutSpreadDistribution = e.PutSpreadDistribution,
            CallSpreadDistribution = e.CallSpreadDistribution,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };
}
