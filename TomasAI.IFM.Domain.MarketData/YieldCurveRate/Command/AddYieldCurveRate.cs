using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command;

public static class AddYieldCurveRate
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    /// <exception cref="AddYieldCurveRateException"></exception>
    public static bool Execute(this AddYieldCurveRateCommand e, YieldCurveRateCommandState state)
        => e switch
        {
            _ when state.YieldCurveRateExists(e.YieldCurveRate.ValueDate, e.Overwrite) => throw new AddYieldCurveRateException(e.YieldCurveRateExistsErrorMsg()),
            _ => state.Update(e.CreateYieldCurveRateAddedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="YieldCurveRateAddedEvent"/> from an <see cref="AddYieldCurveRateCommand"/>.
    /// </summary>
    /// <param name="e">The source add command containing entity identifiers, payload, and origin metadata.</param>
    /// <returns>A fully-populated added event ready to be applied to actor state.</returns>
    internal static YieldCurveRateAddedEvent CreateYieldCurveRateAddedEvent(this AddYieldCurveRateCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, YieldCurveRateAddedEvent.Actor, YieldCurveRateAddedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            YieldCurveRate = e.YieldCurveRate,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };

    /// <summary>
    /// Returns the error message for an attempt to add a yield curve rate that already exists.
    /// </summary>
    internal static string YieldCurveRateExistsErrorMsg(this AddYieldCurveRateCommand e)
        => $"{e.CommandName}: yield curve rate for {e.YieldCurveRate.ValueDate:yyyy-MMM-dd} already exists";

}
