using System.Diagnostics;
using TomasAI.IFM.Domain.MarketData.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event.Actor;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event;

/// <summary>Handles the economic-calendar import event family.</summary>
public static class EconomicCalendarsImported
{
    static EconomicCalendarsImported() => ServiceId = $"{LogSourceType.EconomicCalendarsImported}";

    static string ServiceId { get; }

    /// <summary>Acquires, maps, and durably stores the requested economic-calendar entries.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this EconomicCalendarsImportedEvent @event,
        IEventActorContext context,
        IReferenceDataApi referenceDataApi,
        IDbContextFactory dbFactory,
        ILogger<EconomicCalendarEventActor> logger)
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
        EconomicCalendarsImportedCompleteEvent? complete = null;
        EconomicCalendarsImportedFailEvent? failed = null;
        Exception? processingError = null;
        try
        {
            var importDate = DateOnly.FromDateTime(@event.ImportedDate);
            var countries = NormalizeCountries(@event.CountryCodes);
            var entries = await referenceDataApi.EconomicCalendar.GetAsync(importDate, importDate, countries).ConfigureAwait(false);
            downloaded = entries.Count;
            var records = entries.Select(Map).ToArray();
            Validate(records);
            persisted = null; // A failed bulk write may have accepted a subset.
            await dbFactory.MarketDataDb.InsertEconomicCalendarsAsync(records, @event.DuplicatePolicy, @event.CommandId).ConfigureAwait(false);
            persisted = records.LongLength;
            stopwatch.Stop();
            complete = (EconomicCalendarsImportedCompleteEvent)@event.ToCompleteEvent<EconomicCalendarsImportedCompleteEvent, EconomicCalendarId>(records);
            complete = complete with { Id = terminalId, DownloadOutcome = Outcome(MarketDataDownloadStatus.Completed, null) };
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            processingError = exception;
            logger.LogErrorEvent(ServiceId, exception, "Import processing failed for command {CommandId}", @event.CommandId);
            failed = (EconomicCalendarsImportedFailEvent)@event.ToFailEvent<EconomicCalendarsImportedFailEvent, EconomicCalendarId>(exception);
            failed = failed with { Id = terminalId, DownloadOutcome = Outcome(MarketDataDownloadStatus.Failed, exception) };
        }

        // Publication is outside the acquisition catch: delivery failure cannot invent a Failed download.
        try
        {
            if (complete is not null)
                await context.SendAsync<EconomicCalendarsImportedCompleteEvent, EconomicCalendarId>(complete).ConfigureAwait(false);
            else
                await context.SendAsync<EconomicCalendarsImportedFailEvent, EconomicCalendarId>(failed!).ConfigureAwait(false);
        }
        catch (Exception deliveryError)
        {
            var outcome = complete?.DownloadOutcome ?? failed!.DownloadOutcome!;
            logger.LogError(deliveryError, "Import terminal publication failed. Recovery outcome: {DownloadOutcome}", System.Text.Json.JsonSerializer.Serialize(outcome));
            throw new DownloadLogDeliveryException(outcome, deliveryError);
        }
        if (processingError is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(processingError).Throw();
        return true;

        MarketDataDownloadOutcome Outcome(MarketDataDownloadStatus status, Exception? error) => new()
        {
            Dataset = MarketDataDownloadDataset.EconomicCalendar, ValueDate = DateOnly.FromDateTime(@event.ImportedDate),
            Scope = MarketDataDownloadOutcome.CanonicalScope(@event.CountryCodes), ImportCommandId = @event.CommandId, SourceTerminalEventId = terminalId,
            RequestedAtUtc = MarketDataDownloadOutcome.MillisecondUtc(@event.RequestedOn), StartedAtUtc = started,
            FinishedAtUtc = MarketDataDownloadOutcome.MillisecondUtc(DateTime.UtcNow), Status = status,
            DownloadedRecordCount = downloaded, PersistedRecordCount = persisted, ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            ErrorCode = error is null ? null : "ImportProcessingFailed",
            ErrorMessage = error is null ? null : "Provider acquisition, response validation or data persistence failed. See the correlated import diagnostics."
        };
    }

    /// <summary>Handles the successful terminal event without starting another operation.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this EconomicCalendarsImportedCompleteEvent @event,
        IEventActorContext context,
        ILogger<EconomicCalendarEventActor> logger)
        => DownloadLogDelivery.ForwardAsync(@event.DownloadOutcome, @event,
            MarketDataDownloadDataset.EconomicCalendar, MarketDataDownloadStatus.Completed, context, logger);

    /// <summary>Logs the failed terminal event without retrying the import attempt.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this EconomicCalendarsImportedFailEvent @event,
        IEventActorContext context,
        ILogger<EconomicCalendarEventActor> logger)
        => DownloadLogDelivery.ForwardAsync(@event.DownloadOutcome, @event,
            MarketDataDownloadDataset.EconomicCalendar, MarketDataDownloadStatus.Failed, context, logger);

    /// <summary>Maps a provider-neutral calendar entry to the durable domain schema.</summary>
    static EconomicCalendarReadModel Map(EconomicCalendarEntry entry) => new(
        entry.EventTimeUtc.UtcDateTime,
        entry.CountryCode,
        entry.EventName,
        entry.Actual,
        entry.Forecast,
        entry.Previous,
        entry.RetrievedAtUtc.UtcDateTime,
        entry.Source,
        entry.Impact,
        entry.Unit,
        entry.Change,
        entry.ChangePercentage);

    /// <summary>Validates every mapped row before any durable write is attempted.</summary>
    static void Validate(EconomicCalendarReadModel[] records)
    {
        var rules = new EconomicCalendarValidationRules();
        var errors = records.SelectMany(rules.Execute).ToArray();
        if (errors.Length > 0)
            throw new ArgumentException(string.Join("; ", errors.Select(error => error.ErrorMessage)), nameof(records));
    }

    /// <summary>Normalizes optional country filters for the provider-neutral API.</summary>
    static IReadOnlySet<string>? NormalizeCountries(string[] countryCodes)
    {
        ArgumentNullException.ThrowIfNull(countryCodes);
        if (countryCodes.Length == 0)
            return null;
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var countryCode in countryCodes)
        {
            var normalized = countryCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized)
                || normalized.Length is < 2 or > 3
                || normalized.Any(character => !char.IsAsciiLetter(character)))
                throw new ArgumentException("Country filters must be two- or three-letter codes.", nameof(countryCodes));
            result.Add(normalized);
        }
        return result;
    }
}
