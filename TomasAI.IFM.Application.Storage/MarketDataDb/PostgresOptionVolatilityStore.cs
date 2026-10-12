using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using Npgsql;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Materializes immutable volatility evidence and its monotonic latest pointer on one PostgreSQL transaction.</summary>
public static class PostgresOptionVolatilityStore
{
    /// <summary>Atomically accepts an immutable observation and its dated history row.</summary>
    public static async Task AppendAsync(IEnlistedPostgresTransaction transaction, string environment, OptionIvObservation observation, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(observation);
        MarketDataDbContextExtensions.ValidateEnvironment(environment);
        MarketDataDbContextExtensions.ValidateObservation(observation);
        if (observation.Revision > 1)
        {
            var previous = await transaction.QueryAsync(MarketDataDbSql.SelectObservationById,Values(environment,observation.SupersedesObservationId!),
                row => MarketDataDbContextExtensions.Deserialize<OptionIvObservation>(row.GetFieldValue<byte[]>(0)),token)
            .ConfigureAwait(false);
            if (previous.Count == 0 || previous[0].Series != observation.Series || previous[0].ExchangeValueDate != observation.ExchangeValueDate
                || !string.Equals(previous[0].SamplingSlot,observation.SamplingSlot,StringComparison.Ordinal) || previous[0].Revision >= observation.Revision)
                throw new InvalidOperationException("An observation revision must supersede accepted immutable evidence.");
        }
        var payload = MarketDataDbContextExtensions.Serialize(observation);
        await ImmutableAsync(transaction,MarketDataDbSql.InsertObservationById,Values(environment,observation.ObservationId,payload),token)
            .ConfigureAwait(false);
        await ImmutableAsync(transaction,MarketDataDbSql.InsertObservationHistory,Values(environment,observation.Series.SeriesId,observation.Series.MethodologyVersion,
            VolatilityCalendarBucket.From(observation.ExchangeValueDate).Value,observation.ExchangeValueDate,observation.SamplingSlot,observation.Revision,observation.ObservationId,observation.AvailableAtUtc.UtcDateTime,payload),token)
            .ConfigureAwait(false);
    }

    /// <summary>Atomically accepts all source observations, the metric snapshot/history, and the monotonic current pointer.</summary>
    public static async Task PublishAsync(IEnlistedPostgresTransaction transaction, OptionIvPublication publication, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(publication);
        MarketDataDbContextExtensions.ValidatePublication(publication);
        var metric = publication.Metric;
        var snapshot = metric.Snapshot;
        var scope = string.Join(":",publication.Environment,snapshot.Series.SeriesId,snapshot.Series.MethodologyVersion,snapshot.MetricPolicyVersion);
        await transaction.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended($1,0));",["option-volatility:"+scope],token)
            .ConfigureAwait(false);
        foreach(var observation in publication.SourceObservations.OrderBy(row => row.Revision).ThenBy(row => row.ObservationId,StringComparer.Ordinal))
            await AppendAsync(transaction,publication.Environment,observation,token)
            .ConfigureAwait(false);
        if (metric.Revision > 1)
        {
            var previous = await transaction.QueryAsync(MarketDataDbSql.SelectSnapshot,Values(publication.Environment,metric.SupersedesSnapshotId!),
                row => MarketDataDbContextExtensions.Deserialize<OptionIvMetricRevision>(row.GetFieldValue<byte[]>(2)),token)
            .ConfigureAwait(false);
            if (previous.Count == 0 || previous[0].Snapshot.Series != snapshot.Series
                || !string.Equals(previous[0].Snapshot.MetricPolicyVersion,snapshot.MetricPolicyVersion,StringComparison.Ordinal) || previous[0].Revision >= metric.Revision)
                throw new InvalidOperationException("A metric revision must supersede accepted evidence in the same policy.");
        }
        var payload = MarketDataDbContextExtensions.Serialize(metric);
        await ImmutableAsync(transaction,MarketDataDbSql.InsertSnapshot,Values(publication.Environment,snapshot.SnapshotId,snapshot.SnapshotDigest,metric.PublicationSequence,payload),token)
            .ConfigureAwait(false);
        await ImmutableAsync(transaction,MarketDataDbSql.InsertMetricHistory,Values(publication.Environment,snapshot.Series.SeriesId,snapshot.Series.MethodologyVersion,snapshot.MetricPolicyVersion,
            VolatilityCalendarBucket.From(snapshot.ExchangeValueDate).Value,snapshot.ExchangeValueDate,snapshot.SamplingSlot,metric.Revision,snapshot.SnapshotId,snapshot.AvailableAtUtc.UtcDateTime,metric.PublicationSequence,payload),token)
            .ConfigureAwait(false);
        var pointer = MarketDataDbContextExtensions.Pointer(publication);
        var existing = await transaction.QueryAsync(MarketDataDbSql.SelectLatest,Values(publication.Environment,snapshot.Series.SeriesId,snapshot.Series.MethodologyVersion,snapshot.MetricPolicyVersion),
            row => (Id:row.GetString(0),Digest:row.GetString(1),Sequence:row.GetInt64(2),Available:row.GetDateTime(3)),token)
            .ConfigureAwait(false);
        if (existing.Count != 0 && existing[0].Sequence == metric.PublicationSequence
            && (!string.Equals(existing[0].Id,snapshot.SnapshotId,StringComparison.Ordinal) || !string.Equals(existing[0].Digest,snapshot.SnapshotDigest,StringComparison.Ordinal) || existing[0].Available != pointer.AvailableAtUtc.UtcDateTime))
            throw new InvalidOperationException("Publication sequence conflicts with a different latest pointer.");
        await transaction.ExecuteAsync(MarketDataDbSql.InsertLatest,Values(publication.Environment,snapshot.Series.SeriesId,snapshot.Series.MethodologyVersion,snapshot.MetricPolicyVersion,
            snapshot.SnapshotId,snapshot.SnapshotDigest,metric.PublicationSequence,pointer.AvailableAtUtc.UtcDateTime),token)
            .ConfigureAwait(false);
    }


    /// <summary>Reads one bounded page after filtering point-in-time evidence and choosing the latest eligible observation revision.</summary>
    public static Task<VolatilityPage<OptionIvObservation>> ReadObservationHistoryAsync(IEnlistedPostgresTransaction transaction, VolatilityHistoryPageRequest request, CancellationToken token = default)
        => ReadHistoryAsync(transaction,request,false,MarketDataDbContextExtensions.Deserialize<OptionIvObservation>,token);

    /// <summary>Reads one bounded page after filtering point-in-time evidence and choosing the latest eligible metric revision.</summary>
    public static Task<VolatilityPage<OptionIvMetricRevision>> ReadMetricHistoryAsync(IEnlistedPostgresTransaction transaction, VolatilityHistoryPageRequest request, CancellationToken token = default)
        => ReadHistoryAsync(transaction,request,true,MarketDataDbContextExtensions.Deserialize<OptionIvMetricRevision>,token);

    static async Task<VolatilityPage<T>> ReadHistoryAsync<T>(IEnlistedPostgresTransaction transaction, VolatilityHistoryPageRequest request, bool metric, Func<byte[],T> decode, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);
        MarketDataDbContextExtensions.ValidatePage(request);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request with { PagingState = null, PageSize = 0 })));
        HistoryCursor? cursor = null;
        if (request.PagingState is not null)
        {
            try { cursor = JsonSerializer.Deserialize<HistoryCursor>(request.PagingState); }
            catch (JsonException exception) { throw new ArgumentException("Invalid volatility history cursor.",nameof(request),exception); }
            if (cursor is null || !string.Equals(cursor.Fingerprint,fingerprint,StringComparison.Ordinal) || cursor.Metric != metric)
                throw new ArgumentException("Volatility history cursor belongs to a different query.",nameof(request));
        }
        List<NpgsqlParameter> values = [Text(request.Scope.Environment),Text(request.Scope.Series.SeriesId),Text(request.Scope.Series.MethodologyVersion)];
        var predicate = "environment=$1 AND series_id=$2 AND methodology_version=$3";
        if (metric) { values.Add(Text(request.Scope.MetricPolicyVersion)); predicate += " AND metric_policy_version=$4"; }
        var first = values.Count + 1;
        values.AddRange([Integer(request.Bucket.Value),Date(request.FromValueDate),Date(request.ToValueDate),Text(request.SamplingSlot),TimestampTz(request.Mode == VolatilityHistoricalMode.AsKnown ? request.KnownAtUtc!.Value.UtcDateTime : null),Date(cursor?.Date),Text(cursor?.Slot),Integer(request.PageSize + 1)]);
        var table = metric ? "option_iv_metric_history" : "option_iv_observation_history";
        var tieBreaker = metric ? "publication_sequence DESC,snapshot_id" : "observation_id";
        // Table and column fragments are fixed by the repository, never supplied by the caller.
        var sql = $"""
            WITH ranked AS (
                SELECT value_date,sampling_slot,payload,
                    row_number() OVER (PARTITION BY value_date,sampling_slot ORDER BY revision DESC,{tieBreaker}) AS ordinal
                FROM market_data.{table}
                WHERE {predicate} AND calendar_bucket=${first} AND value_date>=${first+1} AND value_date<=${first+2}
                    AND (${first+3}::text IS NULL OR sampling_slot=${first+3})
                    AND (${first+4}::timestamptz IS NULL OR available_at_utc<=${first+4})
            )
            SELECT value_date,sampling_slot,payload FROM ranked
            WHERE ordinal=1 AND (${first+5}::date IS NULL OR (value_date,sampling_slot COLLATE "C")>(${first+5},${first+6}::text COLLATE "C"))
            ORDER BY value_date,sampling_slot COLLATE "C" LIMIT ${first+7};
            """;
        var rows = await transaction.QueryAsync(sql,[..values],row => (Date:row.GetFieldValue<DateOnly>(0),Slot:row.GetString(1),Payload:row.GetFieldValue<byte[]>(2)),token)
            .ConfigureAwait(false);
        var visible = rows.Take(request.PageSize).ToArray();
        var next = rows.Count > request.PageSize ? JsonSerializer.SerializeToUtf8Bytes(new HistoryCursor(fingerprint,metric,visible[^1].Date,visible[^1].Slot)) : null;
        return new(visible.Select(row => decode(row.Payload)).ToImmutableArray(),next);
    }

    sealed record HistoryCursor(string Fingerprint, bool Metric, DateOnly Date, string Slot);

    static NpgsqlParameter[] Values(params object?[] items) => (NpgsqlParameter[])new OptionVolatilityParameters(items) { PostgreSql = true }.Bind();
    static async Task ImmutableAsync(IEnlistedPostgresTransaction transaction, string sql, object?[] values, CancellationToken token)
    {
        if (await transaction.ScalarAsync(sql,values,token)
            .ConfigureAwait(false) is null)
            throw new InvalidOperationException("Volatility evidence identity conflicts with different immutable facts.");
    }
}
