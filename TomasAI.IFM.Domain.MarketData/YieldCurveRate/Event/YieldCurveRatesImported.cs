using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    /// <summary>Acquires, maps, and durably stores the requested yield curves.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this YieldCurveRatesImportedEvent @event,
        IEventActorContext context,
        IReferenceDataApi referenceDataApi,
        IDbContextFactory dbFactory,
        ILogger<YieldCurveRateEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(referenceDataApi);
        IsArgumentNull.Check(logger);

        var started = MarketDataDownloadOutcome.MillisecondUtc(DateTime.UtcNow);
        var stopwatch = Stopwatch.StartNew();
        long? downloaded = null;
        long? persisted = 0;
        var terminalId = Guid.NewGuid();
        var provider = (referenceDataApi.TreasuryCurve as ITreasuryCurveIdentity)?.DownloadLogProvider ?? "FMP";
        YieldCurveRatesImportedCompleteEvent? complete = null;
        YieldCurveRatesImportedFailEvent? failed = null;
        var acceptedStream = "market-data/treasury/accepted/" + @event.CommandId.ToString("N");
        try
        {
            complete = await AcceptedImportSnapshot.LoadAsync<YieldCurveRatesImportedCompleteEvent>(dbFactory,acceptedStream).ConfigureAwait(false);
            if (complete is null)
            {
            var importDate = DateOnly.FromDateTime(@event.ImportDate);
            var snapshots = await referenceDataApi.TreasuryCurve.GetRangeAsync(importDate, importDate).ConfigureAwait(false);
            downloaded = snapshots.Count;
            var records = snapshots.Select(Map).ToArray();
            Validate(records);
                ValidateDatabaseIdentity(dbFactory);
                persisted = records.LongLength;
                var accepted = (YieldCurveRatesImportedCompleteEvent)@event.ToCompleteEvent<YieldCurveRatesImportedCompleteEvent, YieldCurveRateEntityId>(records);
                accepted = accepted with { Id = terminalId, DownloadOutcome = CreateOutcome(@event, started, terminalId, stopwatch, downloaded, persisted, provider, MarketDataDownloadStatus.Completed, null) };
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
            failed = (YieldCurveRatesImportedFailEvent)@event.ToFailEvent<YieldCurveRatesImportedFailEvent, YieldCurveRateEntityId>(exception);
            failed = failed with { Id = terminalId, DownloadOutcome = CreateOutcome(@event, started, terminalId, stopwatch, downloaded, persisted, provider, MarketDataDownloadStatus.Failed, exception) };
        }

        await PublishTerminalAsync(context, logger, complete, failed).ConfigureAwait(false);
        // A published failure event is terminal for this acquisition attempt. Acknowledging the
        // source event prevents provider or storage failures from creating a second terminal identity.
        return true;


    }

    /// <summary>Captures the terminal import counts, timing and source identity without changing the acquisition outcome on publication failure.</summary>
    static MarketDataDownloadOutcome CreateOutcome(YieldCurveRatesImportedEvent @event, DateTime started, Guid terminalId,
        Stopwatch stopwatch, long? downloaded, long? persisted, string provider, MarketDataDownloadStatus status, Exception? error) => new()
        {
            Dataset = MarketDataDownloadDataset.TreasuryCurve,
            Provider = provider,
            ValueDate = DateOnly.FromDateTime(@event.ImportDate),
            Scope = "US",
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
    static async Task PersistAcceptedAsync(YieldCurveRatesImportedEvent @event, IPostgresEventTransaction transactions,
        string acceptedStream, YieldCurveRatesImportedCompleteEvent accepted, YieldCurveRateReadModel[] records)
    {
        await transactions.ExecuteAsync(async (transaction, token) =>
                    {
                        await transaction.AppendAsync(acceptedStream,
                            @event.CommandId,accepted,0,token).ConfigureAwait(false);
                        await MarketDataDbContextExtensions.ImportYieldCurvesAsync(transaction,records,@event.DuplicatePolicy,@event.CommandId,accepted.ReceivedOn,token).ConfigureAwait(false);
                        return true;
                    }).ConfigureAwait(false);
    }

    /// <summary>Checks that the accepted facts and their projection use the same PostgreSQL security and database identity before counting any persisted rows.</summary>
    static void ValidateDatabaseIdentity(IDbContextFactory dbFactory)
    {
        if (!string.Equals(dbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ||
            !string.Equals(dbFactory.ActorEventSourceDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ||
            PostgresDatabaseIdentity.Parse(dbFactory.MarketDataDb.ConnectionString) != PostgresDatabaseIdentity.Parse(dbFactory.ActorEventSourceDb.ConnectionString))
            throw new InvalidOperationException("Treasury accepted facts and EventSourceDb must share one PostgreSQL database/security identity.");
    }

    /// <summary>Publishes the terminal acquisition outcome after persistence; delivery failure cannot change the recorded import result.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Delivery failures are rethrown as DownloadLogDeliveryException with the unchanged terminal import outcome.")]
    static async Task PublishTerminalAsync(IEventActorContext context, ILogger<YieldCurveRateEventActor> logger,
        YieldCurveRatesImportedCompleteEvent? complete, YieldCurveRatesImportedFailEvent? failed)
    {
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
            global::TomasAI.IFM.Domain.MarketData.ActorLifecycleDiagnosticLog.Write13(logger, deliveryError, System.Text.Json.JsonSerializer.Serialize(outcome));
            throw new DownloadLogDeliveryException(outcome, deliveryError);
        }
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
