using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
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
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event;

/// <summary>Handles the economic-calendar import event family.</summary>
public static class EconomicCalendarsImported
{
    static EconomicCalendarsImported() => ServiceId = $"{LogSourceType.EconomicCalendarsImported}";

    static string ServiceId { get; }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    /// <summary>Acquires, maps, and durably stores the requested economic-calendar entries.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this EconomicCalendarsImportedEvent @event,
        IEventActorContext context,
        IMarketDataApi marketDataApi,
        IDbContextFactory dbFactory,
        ILogger<EconomicCalendarEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(marketDataApi);
        IsArgumentNull.Check(logger);

        var started = MarketDataDownloadOutcome.MillisecondUtc(DateTime.UtcNow);
        var stopwatch = Stopwatch.StartNew();
        long? downloaded = null;
        long? persisted = 0;
        var terminalId = Guid.NewGuid();
        EconomicCalendarsImportedCompleteEvent? complete = null;
        EconomicCalendarsImportedFailEvent? failed = null;
        var acceptedStream = "market-data/economic-calendar/accepted/" + @event.CommandId.ToString("N");
        try
        {
            complete = await AcceptedImportSnapshot.LoadAsync<EconomicCalendarsImportedCompleteEvent>(dbFactory,acceptedStream).ConfigureAwait(false);
            if (complete is null)
            {
            var importDate = DateOnly.FromDateTime(@event.ImportedDate);
            var countries = NormalizeCountries(@event.CountryCodes);
            var entries = await marketDataApi.ReadEconomicCalendarAsync(importDate, importDate, countries?.ToArray()).ConfigureAwait(false);
            downloaded = entries.Length;
            var records = entries.Select(Map).ToArray();
            Validate(records);
                ValidateDatabaseIdentity(dbFactory);
                persisted = records.LongLength;
                var accepted = (EconomicCalendarsImportedCompleteEvent)@event.ToCompleteEvent<EconomicCalendarsImportedCompleteEvent, EconomicCalendarId>(records);
                accepted = accepted with { Id = terminalId, DownloadOutcome = CreateOutcome(@event, started, terminalId, stopwatch, downloaded, persisted, MarketDataDownloadStatus.Completed, null) };
                var transactions = context.Container.Resolve<IPostgresEventTransaction>();
                try
                {
                    await PersistAcceptedAsync(@event, transactions, acceptedStream, accepted, records).ConfigureAwait(false);
                }
                catch
                {
                    persisted = null; // A commit-transport failure can leave the atomic commit outcome uncertain.
                    throw;
                }
                complete = accepted;
                stopwatch.Stop();

            }
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogErrorEvent(ServiceId, exception, "Import processing failed for command {CommandId}", @event.CommandId);
            failed = (EconomicCalendarsImportedFailEvent)@event.ToFailEvent<EconomicCalendarsImportedFailEvent, EconomicCalendarId>(exception);
            failed = failed with { Id = terminalId, DownloadOutcome = CreateOutcome(@event, started, terminalId, stopwatch, downloaded, persisted, MarketDataDownloadStatus.Failed, exception) };
        }

        await PublishTerminalAsync(context, logger, complete, failed).ConfigureAwait(false);
        // A published failure event is terminal for this acquisition attempt. Acknowledging the
        // source event prevents provider or storage failures from creating a second terminal identity.
        return true;


    }

    /// <summary>Captures the terminal import counts, timing and source identity without changing the acquisition outcome on publication failure.</summary>
    static MarketDataDownloadOutcome CreateOutcome(EconomicCalendarsImportedEvent @event, DateTime started, Guid terminalId,
        Stopwatch stopwatch, long? downloaded, long? persisted, MarketDataDownloadStatus status, Exception? error) => new()
        {
            Dataset = MarketDataDownloadDataset.EconomicCalendar,
            ValueDate = DateOnly.FromDateTime(@event.ImportedDate),
            Scope = MarketDataDownloadOutcome.CanonicalScope(@event.CountryCodes),
            ImportCommandId = @event.CommandId,
            SourceTerminalEventId = terminalId,
            RequestedAtUtc = MarketDataDownloadOutcome.MillisecondUtc(@event.RequestedOn),
            StartedAtUtc = started,
            FinishedAtUtc = MarketDataDownloadOutcome.MillisecondUtc(DateTime.UtcNow),
            Status = status,
            DownloadedRecordCount = downloaded,
            PersistedRecordCount = persisted,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            ErrorCode = error is null ? null : "ImportProcessingFailed",
            ErrorMessage = error is null ? null : "Provider acquisition, response validation or data persistence failed. See the correlated import diagnostics."
        };

    /// <summary>Appends the accepted import and its complete mapped projection together in the shared PostgreSQL transaction.</summary>
    static async Task PersistAcceptedAsync(EconomicCalendarsImportedEvent @event, IPostgresEventTransaction transactions,
        string acceptedStream, EconomicCalendarsImportedCompleteEvent accepted, EconomicCalendarReadModel[] records)
    {
        await transactions.ExecuteAsync(async (transaction, token) =>
                    {
                        await transaction.AppendAsync(acceptedStream,
                            @event.CommandId,accepted,0,token).ConfigureAwait(false);
                        await MarketDataDbContextExtensions.ImportEconomicCalendarsAsync(transaction,records,@event.DuplicatePolicy,@event.CommandId,token).ConfigureAwait(false);
                        return true;
                    }).ConfigureAwait(false);
    }

    /// <summary>Checks that the accepted facts and their projection use the same PostgreSQL security and database identity before counting any persisted rows.</summary>
    static void ValidateDatabaseIdentity(IDbContextFactory dbFactory)
    {
        if (!string.Equals(dbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ||
            !string.Equals(dbFactory.ActorEventSourceDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ||
            PostgresDatabaseIdentity.Parse(dbFactory.MarketDataDb.ConnectionString) != PostgresDatabaseIdentity.Parse(dbFactory.ActorEventSourceDb.ConnectionString))
            throw new InvalidOperationException("Economic-calendar accepted facts and EventSourceDb must share one PostgreSQL database/security identity.");
    }

    /// <summary>Publishes the terminal acquisition outcome after persistence; delivery failure cannot change the recorded import result.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Delivery failures are rethrown as DownloadLogDeliveryException with the unchanged terminal import outcome.")]
    static async Task PublishTerminalAsync(IEventActorContext context, ILogger<EconomicCalendarEventActor> logger,
        EconomicCalendarsImportedCompleteEvent? complete, EconomicCalendarsImportedFailEvent? failed)
    {
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
            global::TomasAI.IFM.Domain.MarketData.ActorLifecycleDiagnosticLog.Write11(logger, deliveryError, System.Text.Json.JsonSerializer.Serialize(outcome));
            throw new DownloadLogDeliveryException(outcome, deliveryError);
        }
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
    static HashSet<string>? NormalizeCountries(string[] countryCodes)
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
