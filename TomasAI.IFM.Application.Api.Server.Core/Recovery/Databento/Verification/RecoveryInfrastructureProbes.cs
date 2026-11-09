using Npgsql;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.Verification;

/// <summary>Checks the shared NATS connection and a functional JetStream account request.</summary>
public sealed class NatsRecoveryInfrastructureProbe(
    NatsConnectionManager connection, INatsJetStreamProducerOptions options)
    : IRecoveryInfrastructureProbe
{
    public string Name => "NATS JetStream";
    public RecoveryInfrastructureKind Kind => RecoveryInfrastructureKind.Nats;

    public async Task<RecoveryProbeResult> CheckAsync(CancellationToken cancellationToken)
    {
        var ping = await connection.ProbeAsync(cancellationToken).ConfigureAwait(false);
        if (ping is null)
        {
            _ = await connection.GetClientAsync(options.Url, cancellationToken).ConfigureAwait(false);
            ping = await connection.ProbeAsync(cancellationToken).ConfigureAwait(false);
        }
        if (ping is null) return new(Name, Kind, false, "Shared NATS connection is not active.");
        var jetStream = await connection.GetJetStreamContextAsync(options.Url, cancellationToken)
            .ConfigureAwait(false);
        _ = await jetStream.GetAccountInfoAsync(cancellationToken).ConfigureAwait(false);
        const string stream = "IFM_RECOVERY_PROBE";
        const string subject = "ifm.recovery.probe";
        _ = await jetStream.CreateOrUpdateStreamAsync(new StreamConfig(stream, [subject])
        {
            MaxMsgs = 16,
            MaxAge = TimeSpan.FromMinutes(1)
        }, cancellationToken).ConfigureAwait(false);
        var acknowledgement = await jetStream.PublishAsync(subject, new byte[] { 1 },
            serializer: new NatsByteArrayMessageSerializer(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        acknowledgement.EnsureSuccess();
        return new(Name, Kind, true, "Shared connection and isolated JetStream publish acknowledgement succeeded.");
    }
}

/// <summary>Checks database identity, schema, transactional query, and isolated temporary write/read.</summary>
public sealed class PostgreSqlRecoveryInfrastructureProbe(
    string logicalName, string connectionString, string requiredObject,
    bool isProcedure = false)
    : IRecoveryInfrastructureProbe
{
    public string Name => "PostgreSQL " + logicalName;
    public RecoveryInfrastructureKind Kind => RecoveryInfrastructureKind.PostgreSql;

    public async Task<RecoveryProbeResult> CheckAsync(CancellationToken cancellationToken)
    {
        var expected = new NpgsqlConnectionStringBuilder(connectionString).Database;
        await using var db = new PostgresObjectDataRepositoryConnection()
            .As<NpgsqlConnection>(connectionString);
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var identity = new NpgsqlCommand(isProcedure
            ? "SELECT current_database(), to_regprocedure(@required)::text"
            : "SELECT current_database(), to_regclass(@required)::text", db, transaction);
        identity.Parameters.AddWithValue("required", requiredObject);
        await using (var reader = await identity.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                || reader.GetString(0) != expected || reader.IsDBNull(1))
                return new(Name, Kind, false, "Database identity or required relation differs.");
        }
        await using var write = new NpgsqlCommand(
            "CREATE TEMP TABLE ifm_recovery_probe (token uuid PRIMARY KEY) ON COMMIT DROP; "
            + "INSERT INTO ifm_recovery_probe (token) VALUES (@token); "
            + "SELECT count(*) FROM ifm_recovery_probe WHERE token=@token", db, transaction);
        write.Parameters.AddWithValue("token", Guid.NewGuid());
        var count = Convert.ToInt64(await write.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return new(Name, Kind, count == 1,
            count == 1 ? "Database identity, schema, transaction and isolated write/read succeeded."
                : "Temporary transactional write/read failed.");
    }
}

/// <summary>Checks Scylla keyspace/schema and an isolated expiring write/read.</summary>
public sealed class ScyllaRecoveryInfrastructureProbe(string logicalName, string connectionString)
    : IRecoveryInfrastructureProbe, IAsyncDisposable
{
    readonly ScyllaRecoveryCapabilityProbe capability = new(connectionString);
    public string Name => "ScyllaDB " + logicalName;
    public RecoveryInfrastructureKind Kind => RecoveryInfrastructureKind.ScyllaDb;

    public async Task<RecoveryProbeResult> CheckAsync(CancellationToken cancellationToken)
    {
        var qualified = await capability.CheckAsync(cancellationToken).ConfigureAwait(false);
        return new(Name, Kind, qualified,
            qualified ? "Keyspace, recovery table and isolated TTL write/read succeeded."
                : "Scylla keyspace, schema or isolated write/read did not qualify.");
    }

    public ValueTask DisposeAsync() => capability.DisposeAsync();
}
