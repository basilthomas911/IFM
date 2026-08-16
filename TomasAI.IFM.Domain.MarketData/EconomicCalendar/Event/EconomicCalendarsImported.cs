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

        try
        {
            var importDate = DateOnly.FromDateTime(@event.ImportedDate);
            var countries = NormalizeCountries(@event.CountryCodes);
            var entries = await referenceDataApi.EconomicCalendar
                .GetAsync(importDate, importDate, countries)
                .ConfigureAwait(false);
            var records = entries.Select(Map).ToArray();
            Validate(records);
            await dbFactory.MarketDataDb.InsertEconomicCalendarsAsync(
                records, @event.DuplicatePolicy, @event.CommandId).ConfigureAwait(false);
            var complete = @event.ToCompleteEvent<
                EconomicCalendarsImportedCompleteEvent,
                EconomicCalendarId>(records);
            await context.SendAsync<EconomicCalendarsImportedCompleteEvent, EconomicCalendarId>(
                (EconomicCalendarsImportedCompleteEvent)complete).ConfigureAwait(false);
            logger.LogInformationEvent(ServiceId,
                "Economic-calendar import completed for command {CommandId} with {RecordCount} records.",
                @event.CommandId, records.Length);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogErrorEvent(ServiceId, exception,
                "Economic-calendar import failed for command {CommandId}", @event.CommandId);
            var failed = @event.ToFailEvent<EconomicCalendarsImportedFailEvent, EconomicCalendarId>(exception);
            await context.SendAsync<EconomicCalendarsImportedFailEvent, EconomicCalendarId>(
                (EconomicCalendarsImportedFailEvent)failed).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Handles the successful terminal event without starting another operation.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this EconomicCalendarsImportedCompleteEvent @event,
        IEventActorContext context,
        ILogger<EconomicCalendarEventActor> logger)
    {
        IsArgumentNull.Check(@event);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(logger);
        return ValueTask.FromResult(true);
    }

    /// <summary>Logs the failed terminal event without retrying the import attempt.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this EconomicCalendarsImportedFailEvent @event,
        IEventActorContext context,
        ILogger<EconomicCalendarEventActor> logger)
    {
        IsArgumentNull.Check(@event);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(logger);
        logger.LogErrorEvent(ServiceId,
            "{EventName} for command {CommandId}: {ErrorMessage}",
            @event.EventName, @event.CommandId, @event.ErrorMessage);
        return ValueTask.FromResult(true);
    }

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
