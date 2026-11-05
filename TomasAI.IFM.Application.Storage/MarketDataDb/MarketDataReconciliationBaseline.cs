using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Captures verified PostgreSQL rows as the cutover snapshots for legacy MarketData command streams.</summary>
public sealed class MarketDataReconciliationBaseline(IPostgresEventTransaction transactions)
{
    internal const string BaselineStream = "migration/market-data/postgres-baseline/v1";

    /// <summary>Checks whether the baseline transaction has committed.</summary>
    public static async Task<bool> IsCapturedAsync(IEventSourceActorDbContext database, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var stream = await database.GetEventStreamIdFromDbAsync(BaselineStream).WaitAsync(token).ConfigureAwait(false);
        return stream is not null &&
            await database.GetCommittedStreamVersionAsync(stream.EventStreamId, token).ConfigureAwait(false) > 0;
    }

    /// <summary>Appends a baseline once. The caller must first stop and drain all legacy MarketData writers.</summary>
    public Task<bool> CaptureAsync(bool writersStopped, CancellationToken cancellationToken = default)
    {
        if (!writersStopped)
            throw new InvalidOperationException("Drain all MarketData command and import writers before capturing a baseline.");

        return transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.ExecuteAsync("""
                LOCK TABLE event_stream_id, event_log, market_data.economic_calendar,
                    market_data.economic_calendar_country_code, market_data.yield_curve_rates,
                    market_data.yield_curve_rate_by_date, market_data.yield_curve_rate_year
                    IN SHARE ROW EXCLUSIVE MODE;
                """, [], token).ConfigureAwait(false);
            if (await transaction.ScalarAsync(
                    "SELECT currentversion FROM event_stream_id WHERE eventstream = $1 AND currentversion > 0;",
                    [BaselineStream], token).ConfigureAwait(false) is not null)
                return false;

            var calendar = await transaction.QueryAsync("""
                SELECT eventDate, countryCode, eventName, actual, forecast, prior, impact, unit,
                       change, changePercentage, createdOn, createdBy
                FROM market_data.economic_calendar
                ORDER BY countryCode, eventDate, eventName;
                """, [], reader => MarketDataDbContext.MapToEconomicCalendar(new AdoNetDataRecord().SetReader(reader)), token)
                .ConfigureAwait(false);
            var curves = await ReadCurvesAsync(transaction, MarketDataDbSql.GetCanonicalYieldCurvesForRebuild, token)
                .ConfigureAwait(false);
            var indexed = await ReadCurvesAsync(transaction, MarketDataDbSql.GetIndexedYieldCurvesForRebuild, token)
                .ConfigureAwait(false);
            if (!curves.SequenceEqual(indexed))
                throw new InvalidDataException("Canonical and indexed Treasury curve rows differ.");

            var calendarKeys = calendar.GroupBy(row => row.Id.Format(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToDictionary(row => row.Id), StringComparer.Ordinal);
            var yieldKeys = curves.GroupBy(row => new YieldCurveRateEntityId(row.ValueDate.Year).Format(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var baselineId = Guid.NewGuid();
            var capturedAt = DateTime.UtcNow;

            var calendarHeads = await ReadHeadsAsync(transaction, AddEconomicCalendarCommand.Actor, token).ConfigureAwait(false);
            foreach (var key in calendarHeads.Keys.Union(calendarKeys.Keys, StringComparer.Ordinal))
            {
                var subject = new ActorSubject(ActorType.Event, EconomicCalendarAddedEvent.Actor, "StateSnapshotted", key);
                await transaction.AppendAsync(CommandStream(AddEconomicCalendarCommand.Actor, key), baselineId,
                    new EconomicCalendarStateSnapshottedEvent
                    {
                        Id = Guid.NewGuid(), CommandId = baselineId, Subject = subject, AggregateId = key,
                        ReceivedOn = capturedAt, EventSource = nameof(MarketDataReconciliationBaseline),
                        Entries = calendarKeys.GetValueOrDefault(key) ?? new Dictionary<EconomicCalendarId, EconomicCalendarReadModel>()
                    }, calendarHeads.GetValueOrDefault(key), token).ConfigureAwait(false);
            }

            var yieldHeads = await ReadHeadsAsync(transaction, AddYieldCurveRateCommand.Actor, token).ConfigureAwait(false);
            foreach (var key in yieldHeads.Keys.Union(yieldKeys.Keys, StringComparer.Ordinal))
            {
                var rates = yieldKeys.GetValueOrDefault(key) ?? [];
                var subject = new ActorSubject(ActorType.Event, YieldCurveRateAddedEvent.Actor, "StateSnapshotted", key);
                await transaction.AppendAsync(CommandStream(AddYieldCurveRateCommand.Actor, key), baselineId,
                    new YieldCurveRateStateSnapshottedEvent
                    {
                        Id = Guid.NewGuid(), CommandId = baselineId, Subject = subject, AggregateId = key,
                        ReceivedOn = capturedAt, EventSource = nameof(MarketDataReconciliationBaseline),
                        ValueDates = rates.Select(rate => rate.ValueDate).ToArray(), BaselineRates = rates
                    }, yieldHeads.GetValueOrDefault(key), token).ConfigureAwait(false);
            }

            await transaction.AppendAsync(BaselineStream, baselineId, new MarketDataReconciliationBaselineEvent
            {
                Id = baselineId, CommandId = baselineId, Subject = new(ActorType.Event, "MarketDataMigration", "PostgresBaseline", "v1"),
                ReceivedOn = capturedAt, CalendarRows = calendar.Count, YieldCurveRows = curves.Count,
                EventSource = nameof(MarketDataReconciliationBaseline)
            }, 0, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    private static Task<IReadOnlyList<YieldCurveRateReadModel>> ReadCurvesAsync(
        EnlistedEventTransaction transaction, string query, CancellationToken token) =>
        transaction.QueryAsync(query, [], reader => MarketDataDbContext.MapToYieldCurveRate(new AdoNetDataRecord().SetReader(reader)), token);

    private static async Task<Dictionary<string, long>> ReadHeadsAsync(
        EnlistedEventTransaction transaction, string actor, CancellationToken token)
    {
        var prefix = CommandStream(actor, string.Empty);
        var heads = await transaction.QueryAsync("""
            SELECT eventstream, currentversion FROM event_stream_id
            WHERE starts_with(eventstream, $1) AND currentversion > 0
            ORDER BY eventstream;
            """, [prefix], reader => (Key: reader.GetString(0)[prefix.Length..], Version: reader.GetInt64(1)), token)
            .ConfigureAwait(false);
        return heads.ToDictionary(head => head.Key, head => head.Version, StringComparer.Ordinal);
    }

    private static string CommandStream(string actor, string entity) => $"Command.{actor}.{entity}";
}
