using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.MarketData.Exceptions;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command;

public static class ChangeYieldCurveRate
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    /// <exception cref="ChangeYieldCurveRateException"></exception>
    public static bool Execute(this ChangeYieldCurveRateCommand e, YieldCurveRateCommandState state)
        => e switch
        {
            _ when state.YieldCurveRateDoesNotExist(e.YieldCurveRate.ValueDate, e.Overwrite) => throw new ChangeYieldCurveRateException(e.YieldCurveRateDoesNotExistErrorMsg()),
            _ => state.Update(e.CreateYieldCurveRateChangedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="YieldCurveRateChangedEvent"/> from a <see cref="ChangeYieldCurveRateCommand"/>.
    /// </summary>
    /// <param name="e">The source change command containing entity identifiers, payload, and origin metadata.</param>
    /// <returns>A fully-populated changed event ready to be applied to actor state.</returns>
    internal static YieldCurveRateChangedEvent CreateYieldCurveRateChangedEvent(this ChangeYieldCurveRateCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, YieldCurveRateChangedEvent.Actor, YieldCurveRateChangedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            YieldCurveRate = e.YieldCurveRate,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };

    /// <summary>
    /// Returns the error message for an attempt to change a yield curve rate that does not exist.
    /// </summary>
    internal static string YieldCurveRateDoesNotExistErrorMsg(this ChangeYieldCurveRateCommand e)
        => $"{e.CommandName}: yield curve rate for {e.YieldCurveRate.ValueDate:yyyy-MMM-dd} does not exist";

}
