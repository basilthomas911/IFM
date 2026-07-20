using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.MarketData.Exceptions;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command;

public static class RemoveYieldCurveRate
{
    /// <summary>
    /// Removes a yield curve rate from the specified command state if it exists.
    /// </summary>
    /// <param name="e">The remove command containing the details of the yield curve rate to be removed.</param>
    /// <param name="state">The current state of the yield curve rate commands to update.</param>
    /// <returns>true if the yield curve rate was successfully removed; otherwise, false.</returns>
    /// <exception cref="RemoveYieldCurveRateException">Thrown if the specified yield curve rate does not exist in the current state.</exception>
    public static bool Execute(this RemoveYieldCurveRateCommand e, YieldCurveRateCommandState state)
        => e switch
        {
            _ when state.YieldCurveRateDoesNotExist(e.ValueDate, e.Overwrite) => throw new RemoveYieldCurveRateException(e.YieldCurveRateDoesNotExistErrorMsg()),
            _ => state.Update(e.CreateYieldCurveRateRemovedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="YieldCurveRateRemovedEvent"/> from a <see cref="RemoveYieldCurveRateCommand"/>.
    /// </summary>
    /// <param name="e">The source remove command containing entity identifiers and origin metadata.</param>
    /// <returns>A fully-populated removed event ready to be applied to actor state.</returns>
    internal static YieldCurveRateRemovedEvent CreateYieldCurveRateRemovedEvent(this RemoveYieldCurveRateCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, YieldCurveRateRemovedEvent.Actor, YieldCurveRateRemovedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            ValueDate = e.ValueDate,
            DeletedOn = e.OriginatedOn,
            DeletedBy = e.OriginatedBy
        };

    /// <summary>
    /// Returns the error message for an attempt to remove a yield curve rate that does not exist.
    /// </summary>
    internal static string YieldCurveRateDoesNotExistErrorMsg(this RemoveYieldCurveRateCommand e)
        => $"{e.CommandName}: yield curve rate for {e.ValueDate:yyyy-MMM-dd} does not exist";
}
