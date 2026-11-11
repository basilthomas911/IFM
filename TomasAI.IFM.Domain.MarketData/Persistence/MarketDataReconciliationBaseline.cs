using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Persistence;

/// <summary>Captures verified PostgreSQL rows as the cutover snapshots for legacy MarketData command streams.</summary>
public sealed class MarketDataReconciliationBaseline(IPostgresEventTransaction transactions, TimeProvider? clock = null)
{
    internal const string BaselineStream = "migration/market-data/postgres-baseline/v1";

    /// <summary>Checks whether the baseline transaction has committed.</summary>
    public static async Task<bool> IsCapturedAsync(IEventSourceActorDbContext database, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var stream = await database.GetEventStreamIdFromDbAsync(BaselineStream).WaitAsync(token)
            .ConfigureAwait(false);
        return stream is not null &&
            await database.GetCommittedStreamVersionAsync(stream.EventStreamId, token)
            .ConfigureAwait(false) > 0;
    }

    /// <summary>Appends a baseline once. The caller must first stop and drain all legacy MarketData writers.</summary>
    public Task<bool> CaptureAsync(bool writersStopped, CancellationToken cancellationToken = default)
    {
        if (!writersStopped)
            throw new InvalidOperationException("Drain all MarketData command and import writers before capturing a baseline.");

        return transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.LockMarketDataBaselineAsync(token).ConfigureAwait(false);
            if (await transaction.HasMarketDataBaselineAsync(BaselineStream, token).ConfigureAwait(false))
                return false;

            var calendar = await transaction.ReadBaselineEconomicCalendarAsync(token).ConfigureAwait(false);
            var curves = await transaction.ReadBaselineYieldCurvesAsync(indexed: false, token).ConfigureAwait(false);
            var indexed = await transaction.ReadBaselineYieldCurvesAsync(indexed: true, token).ConfigureAwait(false);
            if (!curves.SequenceEqual(indexed))
                throw new InvalidDataException("Canonical and indexed Treasury curve rows differ.");

            var calendarKeys = calendar.GroupBy(row => row.Id.Format(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToDictionary(row => row.Id), StringComparer.Ordinal);
            var yieldKeys = curves.GroupBy(row => new YieldCurveRateEntityId(row.ValueDate.Year).Format(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var baselineId = Guid.NewGuid();
            var capturedAt = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

            var calendarHeads = await ReadHeadsAsync(transaction, AddEconomicCalendarCommand.Actor, token)
            .ConfigureAwait(false);
            foreach (var key in calendarHeads.Keys.Union(calendarKeys.Keys, StringComparer.Ordinal))
            {
                var subject = new ActorSubject(ActorType.Event, EconomicCalendarAddedEvent.Actor, "StateSnapshotted", key);
                await transaction.AppendAsync(new EnlistedEventAppend(CommandStream(AddEconomicCalendarCommand.Actor, key), baselineId,
                    new EconomicCalendarStateSnapshottedEvent
                    {
                        Id = Guid.NewGuid(), CommandId = baselineId, Subject = subject, AggregateId = key,
                        ReceivedOn = capturedAt, EventSource = nameof(MarketDataReconciliationBaseline),
                        Entries = calendarKeys.GetValueOrDefault(key) ?? new Dictionary<EconomicCalendarId, EconomicCalendarReadModel>()
                    }, calendarHeads.GetValueOrDefault(key)), token)
            .ConfigureAwait(false);
            }

            var yieldHeads = await ReadHeadsAsync(transaction, AddYieldCurveRateCommand.Actor, token)
            .ConfigureAwait(false);
            foreach (var key in yieldHeads.Keys.Union(yieldKeys.Keys, StringComparer.Ordinal))
            {
                var rates = yieldKeys.GetValueOrDefault(key) ?? [];
                var subject = new ActorSubject(ActorType.Event, YieldCurveRateAddedEvent.Actor, "StateSnapshotted", key);
                await transaction.AppendAsync(new EnlistedEventAppend(CommandStream(AddYieldCurveRateCommand.Actor, key), baselineId,
                    new YieldCurveRateStateSnapshottedEvent
                    {
                        Id = Guid.NewGuid(), CommandId = baselineId, Subject = subject, AggregateId = key,
                        ReceivedOn = capturedAt, EventSource = nameof(MarketDataReconciliationBaseline),
                        ValueDates = rates.Select(rate => rate.ValueDate).ToArray(), BaselineRates = rates
                    }, yieldHeads.GetValueOrDefault(key)), token)
            .ConfigureAwait(false);
            }

            await transaction.AppendAsync(new EnlistedEventAppend(BaselineStream, baselineId, new MarketDataReconciliationBaselineEvent
            {
                Id = baselineId, CommandId = baselineId, Subject = new(ActorType.Event, "MarketDataMigration", "PostgresBaseline", "v1"),
                ReceivedOn = capturedAt, CalendarRows = calendar.Count, YieldCurveRows = curves.Count,
                EventSource = nameof(MarketDataReconciliationBaseline)
            }, 0), token)
            .ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    private static async Task<Dictionary<string, long>> ReadHeadsAsync(
        EnlistedEventTransaction transaction, string actor, CancellationToken token)
    {
        var prefix = CommandStream(actor, string.Empty);
        var heads = await transaction.ReadMarketDataBaselineHeadsAsync(prefix, token).ConfigureAwait(false);
        return heads.ToDictionary(head => head.Stream[prefix.Length..], head => head.Revision, StringComparer.Ordinal);
    }

    private static string CommandStream(string actor, string entity) => $"Command.{actor}.{entity}";
}
