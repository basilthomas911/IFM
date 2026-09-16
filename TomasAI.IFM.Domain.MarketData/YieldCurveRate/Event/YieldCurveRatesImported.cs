using System.Diagnostics;
using TomasAI.IFM.Domain.MarketData.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
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

        var started = MarketDataDownloadOutcome.MillisecondUtc(DateTime.UtcNow);
        var stopwatch = Stopwatch.StartNew();
        long? downloaded = null;
        long? persisted = 0;
        var terminalId = Guid.NewGuid();
        var provider = (referenceDataApi.TreasuryCurve as ITreasuryCurveIdentity)?.DownloadLogProvider ?? "FMP";
        YieldCurveRatesImportedCompleteEvent? complete = null;
        YieldCurveRatesImportedFailEvent? failed = null;
        try
        {
            var importDate = DateOnly.FromDateTime(@event.ImportDate);
            var snapshots = await referenceDataApi.TreasuryCurve.GetRangeAsync(importDate, importDate).ConfigureAwait(false);
            downloaded = snapshots.Count;
            var records = snapshots.Select(Map).ToArray();
            Validate(records);
            persisted = null; // A failed bulk write may have accepted a subset.
            await dbFactory.MarketDataDb.InsertYieldCurveRatesAsync(records, @event.DuplicatePolicy, @event.CommandId).ConfigureAwait(false);
            persisted = records.LongLength;
            stopwatch.Stop();
            complete = (YieldCurveRatesImportedCompleteEvent)@event.ToCompleteEvent<YieldCurveRatesImportedCompleteEvent, YieldCurveRateEntityId>(records);
            complete = complete with { Id = terminalId, DownloadOutcome = Outcome(MarketDataDownloadStatus.Completed, null) };
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogErrorEvent(ServiceId, exception, "Import processing failed for command {CommandId}", @event.CommandId);
            failed = (YieldCurveRatesImportedFailEvent)@event.ToFailEvent<YieldCurveRatesImportedFailEvent, YieldCurveRateEntityId>(exception);
            failed = failed with { Id = terminalId, DownloadOutcome = Outcome(MarketDataDownloadStatus.Failed, exception) };
        }

        // Publication is outside the acquisition catch: delivery failure cannot invent a Failed download.
        try
        {
            if (complete is not null)
                await context.SendAsync<YieldCurveRatesImportedCompleteEvent, YieldCurveRateEntityId>(complete).ConfigureAwait(false);
            else
                await context.SendAsync<YieldCurveRatesImportedFailEvent, YieldCurveRateEntityId>(failed!).ConfigureAwait(false);
        }
        catch (Exception deliveryError)
        {
            var outcome = complete?.DownloadOutcome ?? failed!.DownloadOutcome!;
            logger.LogError(deliveryError, "Import terminal publication failed. Recovery outcome: {DownloadOutcome}", System.Text.Json.JsonSerializer.Serialize(outcome));
            throw new DownloadLogDeliveryException(outcome, deliveryError);
        }
        // A published failure event is terminal for this acquisition attempt. Acknowledging the
        // source event prevents provider or storage failures from creating a second terminal identity.
        return true;

        MarketDataDownloadOutcome Outcome(MarketDataDownloadStatus status, Exception? error) => new()
        {
            Dataset = MarketDataDownloadDataset.TreasuryCurve, Provider = provider, ValueDate = DateOnly.FromDateTime(@event.ImportDate),
            Scope = "US", ImportCommandId = @event.CommandId, SourceTerminalEventId = terminalId,
            RequestedAtUtc = MarketDataDownloadOutcome.MillisecondUtc(@event.RequestedOn), StartedAtUtc = started,
            FinishedAtUtc = MarketDataDownloadOutcome.MillisecondUtc(DateTime.UtcNow), Status = status,
            DownloadedRecordCount = downloaded, PersistedRecordCount = persisted, ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            ErrorCode = error is null ? null : "ImportProcessingFailed",
            ErrorMessage = error is null ? null : "Provider acquisition, response validation or data persistence failed. See the correlated import diagnostics."
        };
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
