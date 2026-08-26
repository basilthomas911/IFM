using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.HistoricalBootstrap;

/// <summary>Persists resumable historical checkpoints and immutable manifests in PostgreSQL.</summary>
public sealed class PostgresHistoricalBootstrapStore(
    IDbConnectionSettings connectionSettings,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<PostgresHistoricalBootstrapStore>(
        connectionSettings[EventSourceActorDbContext.EventSourceActorDbConnection], logger),
      IHistoricalBootstrapStore
{
    /// <inheritdoc />
    public override IObjectRepository Database => this;

    /// <inheritdoc />
    public ValueTask<HistoricalBootstrapState?> GetAsync(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        new(Database.Use($"{nameof(HistoricalBootstrapSql)}.{nameof(HistoricalBootstrapSql.GetByAttempt)}", HistoricalBootstrapSql.GetByAttempt)
            .SetParameters(new AttemptParameter(attemptId))
            .ExecuteSingleAsync<HistoricalBootstrapState?>(Map, cancellationToken));

    /// <inheritdoc />
    public ValueTask<HistoricalBootstrapState?> GetCompletedByRequestHashAsync(
        string requestSha256,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestSha256);
        return new(Database.Use($"{nameof(HistoricalBootstrapSql)}.{nameof(HistoricalBootstrapSql.GetCompletedByRequestHash)}", HistoricalBootstrapSql.GetCompletedByRequestHash)
            .SetParameters(new HashParameter(requestSha256))
            .ExecuteSingleAsync<HistoricalBootstrapState?>(Map, cancellationToken));
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        HistoricalBootstrapState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        _ = await Database.Use($"{nameof(HistoricalBootstrapSql)}.{nameof(HistoricalBootstrapSql.Save)}", HistoricalBootstrapSql.Save)
            .SetParameters(new SaveParameter(state))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    static HistoricalBootstrapState Map(IObjectDataRecord row) => new()
    {
        BootstrapAttemptId = row.GetGuid(0),
        RequestSha256 = row.GetString(1),
        Status = (HistoricalBootstrapStatus)row.GetShort(2),
        Checkpoint = new HistoricalAcquisitionCheckpoint
        {
            BootstrapAttemptId = row.GetGuid(0),
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
            : JsonSerializer.Deserialize<HistoricalBootstrapAudit>(row.GetString(11))
    };

    readonly record struct AttemptParameter(Guid AttemptId) : IBindValue
    {
        public object Bind() => Values(Uuid(AttemptId));
    }

    readonly record struct HashParameter(string Hash) : IBindValue
    {
        public object Bind() => Values(Text(Hash));
    }

    readonly record struct SaveParameter(HistoricalBootstrapState State) : IBindValue
    {
        public object Bind() => Values(
            Uuid(State.BootstrapAttemptId),
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
