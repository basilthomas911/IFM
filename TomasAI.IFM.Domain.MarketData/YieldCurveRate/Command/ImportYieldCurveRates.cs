using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command;

public static class ImportYieldCurveRates
{
    /// <summary>
    /// Imports a batch of yield curve rates into the specified command state.
    /// </summary>
    /// <param name="e">The import command containing the collection of yield curve rates to import.</param>
    /// <param name="state">The current state of the yield curve rate commands to update.</param>
    /// <returns>true if the yield curve rates were successfully imported; otherwise, false.</returns>
    public static bool Execute(this ImportYieldCurveRatesCommand e, YieldCurveRateCommandState state)
        => state.Update(e.CreateYieldCurveRatesImportedEvent(), e);

    /// <summary>
    /// Creates a <see cref="YieldCurveRatesImportedEvent"/> from an <see cref="ImportYieldCurveRatesCommand"/>.
    /// </summary>
    /// <param name="e">The source import command containing entity identifiers, batch payload, and origin metadata.</param>
    /// <returns>A fully-populated imported event ready to be applied to actor state.</returns>
    internal static YieldCurveRatesImportedEvent CreateYieldCurveRatesImportedEvent(this ImportYieldCurveRatesCommand e)
    => new()
    {
        CommandId = e.CommandId,
        Subject = new ActorSubject(ActorType.Event, YieldCurveRatesImportedEvent.Actor, YieldCurveRatesImportedEvent.Verb, e.EntityId.Format()),
        EntityId = e.EntityId,
        ImportDate = e.ImportDate,
        YieldCurveRates = e.YieldCurveRates,
        ImportedOn = e.OriginatedOn,
        ImportedBy = e.OriginatedBy
    };
}
