using System.Diagnostics;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using System.Data;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>Runs business writes and the existing event-store append on one connection/transaction.</summary>
public interface IPostgresEventTransaction
{
    Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

/// <summary>Shared financial Command/Function unit of work. No transaction is stored in a singleton context.</summary>
public sealed class PostgresEventTransaction(IDbConnectionSettings settings) : IPostgresEventTransaction
{
    readonly string connectionString = settings[EventSourceActorDbContext.EventSourceActorDbConnection].ConnectionString;

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        for(var attempt=0;;attempt++)
        {
            using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.transaction");
            trace?.SetTag("financial.transaction.attempt", attempt + 1);
            var started=Stopwatch.GetTimestamp();
            try
            {
                var result=await ExecuteAttemptAsync(operation,cancellationToken).ConfigureAwait(false);
                trace?.SetTag("financial.transaction.outcome", "committed");
                FinancialTelemetry.Transaction(Stopwatch.GetElapsedTime(started).TotalMilliseconds,"committed");return result;
            }
            catch(PostgresException error) when(attempt<2 && error.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure)
            {
                // Only a confirmed server rollback may replay this database-only delegate.
                // COMMIT uncertainty, uniqueness conflicts, lock timeouts and application refusals never retry here.
                FinancialTelemetry.Transaction(Stopwatch.GetElapsedTime(started).TotalMilliseconds,"rolled_back_retry");
                trace?.SetTag("financial.transaction.outcome", "rolled_back_retry");
                FinancialTelemetry.Retry();
                await Task.Delay(TimeSpan.FromMilliseconds(10*(attempt+1)),cancellationToken).ConfigureAwait(false);
            }
            catch(Exception error)
            {
                var outcome=error switch { FunctionCommitOutcomeUnknownException=>"unknown",OperationCanceledException=>"cancelled",
                    PostgresException { SqlState:PostgresErrorCodes.LockNotAvailable }=>"lock_timeout",
                    PostgresException { SqlState:PostgresErrorCodes.QueryCanceled }=>"statement_timeout",_=>"failed" };
                trace?.SetTag("financial.transaction.outcome", outcome);
                FinancialTelemetry.Transaction(Stopwatch.GetElapsedTime(started).TotalMilliseconds,outcome);throw;
            }
        }
    }

    async Task<T> ExecuteAttemptAsync<T>(Func<EnlistedEventTransaction,CancellationToken,Task<T>> operation,CancellationToken cancellationToken)
    {
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        using (FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.open_connection"))
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var beginTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.begin");
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        beginTrace?.Stop();
        var enlisted = new EnlistedEventTransaction(connection, transaction);
        T result;
        try
        {
            // Limit lock/provider waits; caller cancellation can impose a stricter operation deadline.
            using (FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.configure"))
                await enlisted.ExecuteAsync("SET LOCAL lock_timeout = '1000ms'; SET LOCAL statement_timeout = '2000ms';", [], cancellationToken).ConfigureAwait(false);
            using (FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.apply"))
                result = await operation(enlisted, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            // COMMIT has not been sent. Disposal closes a connection if rollback itself cannot be confirmed.
            try
            {
                using var rollbackTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.rollback");
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch { }
            throw;
        }
        try
        {
            using var commitTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.commit");
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException)
        {
            // A server error response (for example deferred constraint failure) confirms COMMIT failed.
            throw;
        }
        catch (Exception exception)
        {
            throw new FunctionCommitOutcomeUnknownException(
                "PostgreSQL COMMIT was not acknowledged. Reconcile the original operation receipt before retrying.", exception);
        }
        return result; // Cancellation after confirmed commit cannot turn success into rollback.
    }
}

/// <summary>SQL/event operations explicitly enlisted in a single request-owned transaction.</summary>
/// <remarks>Only storage implementations use this surface; actors never execute SQL.</remarks>
public sealed class EnlistedEventTransaction
{
    readonly NpgsqlConnection connection;
    readonly NpgsqlTransaction transaction;
    internal EnlistedEventTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
        => (this.connection, this.transaction) = (connection, transaction);

    /// <summary>Executes parameterized SQL on the enlisted connection.</summary>
    public async Task<int> ExecuteAsync(string sql, object?[] parameters, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a scalar on the same transaction; null remains explicit.</summary>
    public async Task<object?> ScalarAsync(string sql, object?[] parameters, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(sql, parameters);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is DBNull ? null : value;
    }

    /// <summary>Materializes a bounded storage query before disposing its reader.</summary>
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object?[] parameters,
        Func<NpgsqlDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var values = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) values.Add(map(reader));
        return values;
    }

    /// <summary>Appends to the existing event schema using its optimistic stream version and standard serialization.</summary>
    public async Task<long> AppendAsync(string stream, Guid commandId, IEvent domainEvent,
        long expectedStreamVersion, CancellationToken cancellationToken)
    {
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.event.append");
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (commandId == Guid.Empty || expectedStreamVersion < 0) throw new ArgumentException("Invalid event identity/version.");
        var streamId = Convert.ToInt64(await ScalarAsync("SELECT eventstreamid FROM event_stream_id WHERE eventstream=$1;",[stream],cancellationToken).ConfigureAwait(false)
            ?? await ScalarAsync(EventSourceDbSql.InsertEventStreamId, [stream], cancellationToken).ConfigureAwait(false));
        var type = domainEvent.GetType();
        // The existing upsert takes an UPDATE lock even when the registry row is unchanged.
        // Reading pre-registered metadata avoids serializing unrelated Portfolio transactions on an event name.
        var nameId = (int)(await ScalarAsync("SELECT eventnameid FROM event_name_id WHERE eventname=$1 AND eventtypename=$2;",
            [type.Name,type.AssemblyQualifiedName!],cancellationToken).ConfigureAwait(false)
            ?? await ScalarAsync(EventSourceDbSql.InsertEventNameId,[type.Name,type.AssemblyQualifiedName!],cancellationToken).ConfigureAwait(false))!;
        using var serializeTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.event.serialize");
        var payloadBytes = EventLogMessagePackCodec.Shared.Serialize(domainEvent);
        var payload = new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bytea, Value = payloadBytes };
        serializeTrace?.SetTag("event.payload.bytes", payloadBytes.Length);
        serializeTrace?.Stop();
        using var insertTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.event.insert");
        var eventId = await ScalarAsync(EventSourceDbSql.InsertEventLogExpectedVersion,
            [streamId, nameId, payload, commandId, DateTime.UtcNow, expectedStreamVersion], cancellationToken).ConfigureAwait(false);
        insertTrace?.Stop();
        if (eventId is not long id) throw new ConcurrencyException($"Event stream {stream} is not at expected version {expectedStreamVersion}.");
        // EventId is transport/storage metadata. The immutable business event identity remains domainEvent.Id.
        EventInitHelper.SetProperty(domainEvent, nameof(IEvent.EventId), id);
        if (domainEvent is IRequireDurableProjection required)
        {
            var rule = required.RequiredProjection;
            if (rule.InitialStage is not (TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.PublishProcessingEvent
                or TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.ApplyProjection))
                throw new ArgumentException("Invalid durable projection stage.");
            var now = DateTime.UtcNow;
            var marker = await ScalarAsync(EventSourceDbSql.TryCreateEventProjectorExecutionState,
                [id, rule.ActorName, rule.ProjectorName, false, 0, "Processing", rule.InitialStage.ToString(),
                    string.Empty, $"{now:o}", $"{now:o}", now], cancellationToken).ConfigureAwait(false);
            if (marker is null) throw new InvalidOperationException("Required durable projection marker was not persisted.");
        }
        return id;
    }

    NpgsqlCommand CreateCommand(string sql, object?[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 3 };
        foreach (var parameter in parameters)
            command.Parameters.Add(parameter is NpgsqlParameter typed ? typed : new NpgsqlParameter { Value = parameter ?? DBNull.Value });
        return command;
    }
}
