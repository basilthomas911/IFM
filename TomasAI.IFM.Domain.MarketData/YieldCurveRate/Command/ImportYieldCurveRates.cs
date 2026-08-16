using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;

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
    {
        var normalized = new Dictionary<DateOnly, YieldCurveRateReadModel>();
        foreach (var rate in e.YieldCurveRates)
        {
            if (normalized.TryGetValue(rate.ValueDate, out var existing))
            {
                if (existing != rate)
                {
                    throw new MarketDataImportDuplicateException(
                        $"Conflicting yield curves were supplied for {rate.ValueDate:yyyy-MM-dd}.");
                }

                continue;
            }

            if (e.DuplicatePolicy == ImportDuplicatePolicy.Reject
                && state.YieldCurveRateExists(rate.ValueDate, overwrite: false))
            {
                throw new MarketDataImportDuplicateException(
                    $"A yield curve already exists for {rate.ValueDate:yyyy-MM-dd}.");
            }

            normalized.Add(rate.ValueDate, rate);
        }

        return state.Update(
            e.CreateYieldCurveRatesImportedEvent([.. normalized.Values.OrderBy(rate => rate.ValueDate)]),
            e);
    }

    /// <summary>
    /// Creates a <see cref="YieldCurveRatesImportedEvent"/> from an <see cref="ImportYieldCurveRatesCommand"/>.
    /// </summary>
    /// <param name="e">The source import command containing entity identifiers, batch payload, and origin metadata.</param>
    /// <returns>A fully-populated imported event ready to be applied to actor state.</returns>
    internal static YieldCurveRatesImportedEvent CreateYieldCurveRatesImportedEvent(
        this ImportYieldCurveRatesCommand e,
        YieldCurveRateReadModel[]? normalizedRates = null)
    => new()
    {
        CommandId = e.CommandId,
        Subject = new ActorSubject(ActorType.Event, YieldCurveRatesImportedEvent.Actor, YieldCurveRatesImportedEvent.Verb, e.EntityId.Format()),
        EntityId = e.EntityId,
        ImportDate = e.ImportDate,
        YieldCurveRates = normalizedRates ?? e.YieldCurveRates,
        ImportedOn = e.OriginatedOn,
        ImportedBy = e.OriginatedBy,
        DuplicatePolicy = e.DuplicatePolicy
    };
}
