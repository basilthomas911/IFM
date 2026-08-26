using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.HistoricalDataLoader;

/// <summary>Persists resumable historical checkpoints and immutable manifests in PostgreSQL.</summary>
public sealed class PostgresHistoricalDataLoaderStore(
    IDbConnectionSettings connectionSettings,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<PostgresHistoricalDataLoaderStore>(
        connectionSettings[EventSourceActorDbContext.EventSourceActorDbConnection], logger),
      IHistoricalDataLoaderStore
{
    /// <inheritdoc />
    public override IObjectRepository Database => this;

    /// <inheritdoc />
    public ValueTask<HistoricalDataLoaderState?> GetAsync(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        new(Database.Use($"{nameof(HistoricalDataLoaderSql)}.{nameof(HistoricalDataLoaderSql.GetByAttempt)}", HistoricalDataLoaderSql.GetByAttempt)
            .SetParameters(new AttemptParameter(attemptId))
            .ExecuteSingleAsync<HistoricalDataLoaderState?>(Map, cancellationToken));

    /// <inheritdoc />
    public ValueTask<HistoricalDataLoaderState?> GetCompletedByRequestHashAsync(
        string requestSha256,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestSha256);
        return new(Database.Use($"{nameof(HistoricalDataLoaderSql)}.{nameof(HistoricalDataLoaderSql.GetCompletedByRequestHash)}", HistoricalDataLoaderSql.GetCompletedByRequestHash)
            .SetParameters(new HashParameter(requestSha256))
            .ExecuteSingleAsync<HistoricalDataLoaderState?>(Map, cancellationToken));
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        HistoricalDataLoaderState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        _ = await Database.Use($"{nameof(HistoricalDataLoaderSql)}.{nameof(HistoricalDataLoaderSql.Save)}", HistoricalDataLoaderSql.Save)
            .SetParameters(new SaveParameter(state))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    static HistoricalDataLoaderState Map(IObjectDataRecord row) => new()
    {
        DataLoadAttemptId = row.GetGuid(0),
        RequestSha256 = row.GetString(1),
        Status = (HistoricalDataLoaderStatus)row.GetShort(2),
        Checkpoint = new HistoricalAcquisitionCheckpoint
        {
            DataLoadAttemptId = row.GetGuid(0),
            Stage = (HistoricalAcquisitionStage)row.GetShort(3),
            ProviderJobId = row.GetString(4),
            ProviderFileId = row.GetString(5),
            BatchOrdinal = row.GetLong(6),
            SourcePosition = row.GetString(7)
        },
        ErrorMessage = row.GetString(8),
        UpdatedAtUtc = new DateTimeOffset(
            DateTime.SpecifyKind(row.GetDateTime(9), DateTimeKind.Utc)),
        Manifest = row.IsNull(10)
            ? null
            : JsonSerializer.Deserialize<MarketDataHistoricalManifest>(row.GetString(10)),
        Audit = row.IsNull(11)
            ? null
            : JsonSerializer.Deserialize<HistoricalDataLoaderAudit>(row.GetString(11))
    };

    readonly record struct AttemptParameter(Guid AttemptId) : IBindValue
    {
        public object Bind() => Values(Uuid(AttemptId));
    }

    readonly record struct HashParameter(string Hash) : IBindValue
    {
        public object Bind() => Values(Text(Hash));
    }

    readonly record struct SaveParameter(HistoricalDataLoaderState State) : IBindValue
    {
        public object Bind() => Values(
            Uuid(State.DataLoadAttemptId),
            Text(State.RequestSha256),
            Smallint((short)State.Status),
            Smallint((short)State.Checkpoint.Stage),
            Text(State.Checkpoint.ProviderJobId),
            Text(State.Checkpoint.ProviderFileId),
            Bigint(State.Checkpoint.BatchOrdinal),
            Text(State.Checkpoint.SourcePosition),
            Text(State.ErrorMessage),
            TimestampTz(State.UpdatedAtUtc.UtcDateTime),
            Text(State.Manifest is null ? string.Empty : JsonSerializer.Serialize(State.Manifest)),
            Text(State.Audit is null ? string.Empty : JsonSerializer.Serialize(State.Audit)));
    }
}
