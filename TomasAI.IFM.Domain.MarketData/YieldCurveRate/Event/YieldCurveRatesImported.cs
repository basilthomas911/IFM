using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event.Actor;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event;

/// <summary>Handles the yield-curve import event family.</summary>
public static class YieldCurveRatesImported
{
    static YieldCurveRatesImported() => ServiceId = $"{LogSourceType.YieldCurveRatesImported}";

    static string ServiceId { get; }

    /// <summary>Acquires, maps, and durably stores the requested yield curves.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this YieldCurveRatesImportedEvent @event,
        IEventActorContext context,
        IReferenceDataApi referenceDataApi,
        IDbContextFactory dbFactory,
        ILogger<YieldCurveRateEventActor> logger)
    {
        IsArgumentNull.Check(@event);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(referenceDataApi);
        IsArgumentNull.Check(dbFactory);
        IsArgumentNull.Check(logger);

        try
        {
            var importDate = DateOnly.FromDateTime(@event.ImportDate);
            var snapshots = await referenceDataApi.TreasuryCurve
                .GetRangeAsync(importDate, importDate)
                .ConfigureAwait(false);
            var records = snapshots.Select(Map).ToArray();
            Validate(records);
            await dbFactory.MarketDataDb.InsertYieldCurveRatesAsync(
                records, @event.DuplicatePolicy, @event.CommandId).ConfigureAwait(false);
            var complete = @event.ToCompleteEvent<
                YieldCurveRatesImportedCompleteEvent,
                YieldCurveRateEntityId>(records);
            await context.SendAsync<YieldCurveRatesImportedCompleteEvent, YieldCurveRateEntityId>(
                (YieldCurveRatesImportedCompleteEvent)complete).ConfigureAwait(false);
            logger.LogInformationEvent(ServiceId,
                "Yield-curve import completed for command {CommandId} with {RecordCount} records.",
                @event.CommandId, records.Length);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogErrorEvent(ServiceId, exception,
                "Yield-curve import failed for command {CommandId}", @event.CommandId);
            var failed = @event.ToFailEvent<YieldCurveRatesImportedFailEvent, YieldCurveRateEntityId>(exception);
            await context.SendAsync<YieldCurveRatesImportedFailEvent, YieldCurveRateEntityId>(
                (YieldCurveRatesImportedFailEvent)failed).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Handles the successful terminal event without starting another operation.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this YieldCurveRatesImportedCompleteEvent @event,
        IEventActorContext context,
        ILogger<YieldCurveRateEventActor> logger)
    {
        IsArgumentNull.Check(@event);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(logger);
        return ValueTask.FromResult(true);
    }

    /// <summary>Logs the failed terminal event without retrying the import attempt.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this YieldCurveRatesImportedFailEvent @event,
        IEventActorContext context,
        ILogger<YieldCurveRateEventActor> logger)
    {
        IsArgumentNull.Check(@event);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(logger);
        logger.LogErrorEvent(ServiceId,
            "{EventName} for command {CommandId}: {ErrorMessage}",
            @event.EventName, @event.CommandId, @event.ErrorMessage);
        return ValueTask.FromResult(true);
    }

    /// <summary>Maps a provider-neutral Treasury snapshot to the durable domain schema.</summary>
    static YieldCurveRateReadModel Map(TreasuryCurveSnapshot snapshot) => new(
        snapshot.ValueDate,
        Rate(snapshot, TreasuryTenor.OneMonth),
        Rate(snapshot, TreasuryTenor.TwoMonth),
        Rate(snapshot, TreasuryTenor.ThreeMonth),
        Rate(snapshot, TreasuryTenor.SixMonth),
        Rate(snapshot, TreasuryTenor.OneYear),
        Rate(snapshot, TreasuryTenor.TwoYear),
        Rate(snapshot, TreasuryTenor.ThreeYear),
        Rate(snapshot, TreasuryTenor.FiveYear),
        Rate(snapshot, TreasuryTenor.SevenYear),
        Rate(snapshot, TreasuryTenor.TenYear),
        Rate(snapshot, TreasuryTenor.TwentyYear),
        Rate(snapshot, TreasuryTenor.ThirtyYear));

    /// <summary>Validates every mapped row before any durable write is attempted.</summary>
    static void Validate(YieldCurveRateReadModel[] records)
    {
        var rules = new YieldCurveRateValidationRules();
        var errors = records.SelectMany(rules.Execute).ToArray();
        if (errors.Length > 0)
            throw new ArgumentException(string.Join("; ", errors.Select(error => error.ErrorMessage)), nameof(records));
    }

    /// <summary>Gets a required Treasury tenor as percentage points.</summary>
    static double Rate(TreasuryCurveSnapshot snapshot, TreasuryTenor tenor)
    {
        if (!snapshot.TryGetRate(tenor, out var point))
            throw new InvalidOperationException(
                $"Treasury curve {snapshot.ValueDate:yyyy-MM-dd} is missing tenor {tenor}.");
        return decimal.ToDouble(point.RatePercent);
    }
}
