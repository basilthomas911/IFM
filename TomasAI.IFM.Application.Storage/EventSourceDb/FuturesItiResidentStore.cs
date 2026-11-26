using System.Data;
using System.Runtime.CompilerServices;
using Npgsql;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Storage.Postgres;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>Exclusive ITI writer and durable binary input journal. No connection is reconnected or retried implicitly.</summary>
public sealed class FuturesItiResidentStore : IAsyncDisposable
{
    // One ITI command actor owns all of its timeframe streams. Ordinary repositories must take this same fence.
    public const long Fence = 0x49464D495449;
    public const string CreateStatement = """
        CREATE TABLE IF NOT EXISTS futures_iti_input_journal (
            input_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            command_id uuid NOT NULL UNIQUE,
            stream text NOT NULL,
            dataset text NOT NULL,
            publisher_id integer NOT NULL,
            instrument_id bigint NOT NULL,
            stream_epoch uuid NOT NULL,
            trade_ordinal bigint NOT NULL CHECK (trade_ordinal > 0),
            market_ticks bigint NOT NULL,
            payload bytea NOT NULL,
            UNIQUE (stream, dataset, publisher_id, instrument_id, stream_epoch, trade_ordinal)
        );
        CREATE INDEX IF NOT EXISTS ix_futures_iti_input_stream ON futures_iti_input_journal (stream, input_id);
        CREATE TABLE IF NOT EXISTS futures_iti_input_checkpoint (
            stream text PRIMARY KEY,
            input_id bigint NOT NULL REFERENCES futures_iti_input_journal(input_id),
            stream_version bigint NOT NULL,
            updated_at timestamp with time zone NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS futures_iti_journal_root (
            singleton boolean PRIMARY KEY DEFAULT true CHECK(singleton), journal_root uuid NOT NULL
        );
        CREATE TABLE IF NOT EXISTS futures_iti_epoch_seal (
            stream text NOT NULL, epoch uuid NOT NULL, last_ordinal bigint NOT NULL,
            input_id bigint NOT NULL REFERENCES futures_iti_input_journal(input_id),
            PRIMARY KEY(stream,epoch)
        );
        CREATE TABLE IF NOT EXISTS futures_iti_epoch_bridge (
            stream text NOT NULL, from_epoch uuid NOT NULL, to_epoch uuid NOT NULL,
            last_ordinal bigint NOT NULL, input_id bigint NOT NULL,
            PRIMARY KEY(stream,from_epoch), UNIQUE(stream,to_epoch)
        );
        """;
    readonly EventSourceActorDbContext database;
    readonly NpgsqlConnection connection;
    readonly SemaphoreSlim gate = new(1, 1);
    readonly CancellationTokenSource lifetime = new();
    readonly EventLogMessagePackCodec codec = new();
    Task heartbeat = Task.CompletedTask;
    long lastProof;
    int failed, disposed;
    long transactionCount, journalWrites;

    FuturesItiResidentStore(EventSourceActorDbContext database, NpgsqlConnection connection)
        => (this.database, this.connection) = (database, connection);

    public long Transactions => Interlocked.Read(ref transactionCount);
    public long JournalWrites => Interlocked.Read(ref journalWrites);
    public int BackendProcessId => connection.ProcessID;
    public string JournalNamespace
    {
        get
        {
            var source = new NpgsqlConnectionStringBuilder(database.ConnectionString);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{source.Host}|{source.Port}|{source.Database}|{source.Username}"))).Substring(0, 24);
        }
    }
    public bool IsOwned => Volatile.Read(ref failed) == 0 && Volatile.Read(ref disposed) == 0
        && connection.State == ConnectionState.Open
        && System.Diagnostics.Stopwatch.GetElapsedTime(Interlocked.Read(ref lastProof)) < TimeSpan.FromSeconds(5);

    public void AssertOwned()
    {
        if (!IsOwned)
        {
            Interlocked.Exchange(ref failed, 1);
            throw new InvalidOperationException("ITI writer ownership is unavailable; stop intake and reconcile before recovery.");
        }
    }

    public static async Task<FuturesItiResidentStore> OpenAsync(EventSourceActorDbContext database, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(
            new NpgsqlConnectionStringBuilder(database.ConnectionString) { Pooling = false, KeepAlive = 1 }.ConnectionString);
        try
        {
            await connection.OpenAsync(token).ConfigureAwait(false);
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock($1)", connection);
            command.Parameters.AddWithValue(Fence);
            if (await command.ExecuteScalarAsync(token).ConfigureAwait(false) is not true)
                throw new InvalidOperationException("Another API owns the ITI writer fence.");
            var result = new FuturesItiResidentStore(database, connection);
            Interlocked.Exchange(ref result.lastProof, System.Diagnostics.Stopwatch.GetTimestamp());
            result.heartbeat = result.PulseAsync();
            return result;
        }
        catch { await connection.DisposeAsync().ConfigureAwait(false); throw; }
    }

    public async Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation, CancellationToken token = default)
    {
        AssertOwned();
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            AssertOwned();
            await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            var enlisted = new EnlistedEventTransaction(connection, transaction, codec, database._eventLogSqlLayout,
                timeProvider: database.BusinessClock);
            T value;
            try
            {
                value = await operation(enlisted, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
            }
            catch
            {
                try { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); }
                catch { Interlocked.Exchange(ref failed, 1); }
                throw;
            }
            finally { enlisted.Complete(); }
            try { await transaction.CommitAsync(token).ConfigureAwait(false); }
            catch (PostgresException)
            {
                Interlocked.Exchange(ref failed, 1);
                throw; // Server rejected COMMIT; rollback is confirmed, candidate state still requires rehydration.
            }
            catch (Exception error)
            {
                Interlocked.Exchange(ref failed, 1);
                throw new InvalidOperationException("ITI commit outcome must be reconciled from the journal/checkpoint on restart.", error);
            }
            Interlocked.Exchange(ref lastProof, System.Diagnostics.Stopwatch.GetTimestamp());
            Interlocked.Increment(ref transactionCount);
            return value;
        }
        catch
        {
            if (connection.State != ConnectionState.Open) Interlocked.Exchange(ref failed, 1);
            throw;
        }
        finally { gate.Release(); }
    }

    /// <summary>Admits an ordered batch durably before the caller can attach its local processing capability.</summary>
    public Task<IReadOnlyList<long>> AppendInputsAsync(IReadOnlyList<Input> inputs, CancellationToken token = default)
        => ExecuteAsync<IReadOnlyList<long>>(async (transaction, ct) =>
        {
            var inserted = await transaction.QueryAsync("""
                INSERT INTO futures_iti_input_journal
                    (command_id,stream,dataset,publisher_id,instrument_id,stream_epoch,trade_ordinal,market_ticks,payload)
                SELECT * FROM unnest($1::uuid[], $2::text[], $3::text[], $4::integer[], $5::bigint[],
                    $6::uuid[], $7::bigint[], $8::bigint[], $9::bytea[])
                ON CONFLICT (command_id) DO UPDATE SET command_id = EXCLUDED.command_id
                WHERE futures_iti_input_journal.payload = EXCLUDED.payload
                RETURNING command_id,input_id,(xmax=0)
                """, [inputs.Select(input => input.CommandId).ToArray(), inputs.Select(input => input.Stream).ToArray(),
                    inputs.Select(input => input.Dataset).ToArray(), inputs.Select(input => input.PublisherId).ToArray(),
                    inputs.Select(input => input.InstrumentId).ToArray(), inputs.Select(input => input.Epoch).ToArray(),
                    inputs.Select(input => input.Ordinal).ToArray(), inputs.Select(input => input.MarketTicks).ToArray(),
                    inputs.Select(input => input.Payload).ToArray()],
                reader => (Id: reader.GetGuid(0), Sequence: reader.GetInt64(1), IsNew: reader.GetBoolean(2)), ct).ConfigureAwait(false);
            if (inserted.Count != inputs.Count) throw new InvalidOperationException("Conflicting ITI source identity/payload.");
            var identities = inserted.ToDictionary(value => value.Id, value => value.Sequence);
            var result = inputs.Select(input => identities[input.CommandId]).ToArray();
            var fresh = inserted.Where(value => value.IsNew).Select(value => inputs.Single(input => input.CommandId == value.Id)).ToArray();
            if (fresh.Length > 0) _ = await transaction.ExecuteAsync("""
                DELETE FROM futures_iti_epoch_seal seal USING
                    unnest($1::text[], $2::uuid[]) source(stream,epoch)
                WHERE seal.stream=source.stream AND seal.epoch=source.epoch
                """, [fresh.Select(input => input.Stream).ToArray(), fresh.Select(input => input.Epoch).ToArray()], ct).ConfigureAwait(false);
            Interlocked.Add(ref journalWrites, inputs.Count);
            return result;
        }, token);

    const string PendingSql = """
            WITH epoch_first AS MATERIALIZED (
                SELECT stream, stream_epoch, min(input_id) AS first_input_id
                FROM futures_iti_input_journal GROUP BY stream, stream_epoch
            )
            SELECT j.input_id, j.stream, j.payload
            FROM futures_iti_input_journal j
            LEFT JOIN futures_iti_input_checkpoint c ON c.stream = j.stream
            LEFT JOIN futures_iti_input_journal anchor ON anchor.input_id=c.input_id
            JOIN epoch_first first_input ON first_input.stream=j.stream AND first_input.stream_epoch=j.stream_epoch
            WHERE c.input_id IS NULL OR (j.stream_epoch=anchor.stream_epoch AND j.trade_ordinal>anchor.trade_ordinal)
                OR (j.stream_epoch<>anchor.stream_epoch AND j.input_id>c.input_id)
            ORDER BY j.stream,
                CASE WHEN j.stream_epoch=anchor.stream_epoch THEN 0 ELSE 1 END,
                first_input.first_input_id,
                j.trade_ordinal
            """;

    /// <summary>Recovery-only streaming read with a fixed query snapshot and bounded client buffering.</summary>
    public async IAsyncEnumerable<StoredInput> ReadPendingAsync([EnumeratorCancellation] CancellationToken token = default)
    {
        AssertOwned();
        await using var readConnection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(database.ConnectionString);
        await readConnection.OpenAsync(token).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(PendingSql, readConnection) { CommandTimeout = 0 };
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
            yield return new(reader.GetInt64(0), reader.GetString(1), await reader.GetFieldValueAsync<byte[]>(2, token).ConfigureAwait(false));
    }

    public Task<IReadOnlyList<StoredInput>> ReadCheckpointInputsAsync(CancellationToken token = default)
        => ExecuteAsync(async (transaction, ct) => await transaction.QueryAsync("""
            SELECT j.input_id,j.stream,j.payload FROM futures_iti_input_checkpoint c
            JOIN futures_iti_input_journal j ON j.input_id=c.input_id ORDER BY j.input_id
            """, [], reader => new StoredInput(reader.GetInt64(0), reader.GetString(1), (byte[])reader.GetValue(2)), ct).ConfigureAwait(false), token);

    /// <summary>Recovery-only original frozen input proof; ordinary tick decisions never call this query.</summary>
    public Task<IReadOnlyList<StoredInput>> ReadOriginalSourceAsync(string stream, Guid epoch, long ordinal, CancellationToken token = default)
        => ExecuteAsync(async (transaction, ct) => await transaction.QueryAsync("""
            SELECT input_id,stream,payload FROM futures_iti_input_journal
            WHERE stream=$1 AND stream_epoch=$2 AND trade_ordinal=$3
            """, [stream, epoch, ordinal], reader => new StoredInput(reader.GetInt64(0), reader.GetString(1), (byte[])reader.GetValue(2)), ct).ConfigureAwait(false), token);

    public static Task CheckpointAsync(EnlistedEventTransaction transaction, string stream, long inputId, long version, CancellationToken token)
        => transaction.ExecuteAsync("""
            INSERT INTO futures_iti_input_checkpoint(stream,input_id,stream_version) VALUES ($1,$2,$3)
            ON CONFLICT(stream) DO UPDATE SET input_id=EXCLUDED.input_id,
                stream_version=EXCLUDED.stream_version, updated_at=now()
            """, [stream, inputId, version], token);

    public Task<bool> BindJournalRootAsync(Guid root, CancellationToken token = default)
        => ExecuteAsync(async (transaction, ct) =>
        {
            var bound = await transaction.ScalarAsync("""
                INSERT INTO futures_iti_journal_root(singleton,journal_root) VALUES (true,$1)
                ON CONFLICT(singleton) DO UPDATE SET journal_root=futures_iti_journal_root.journal_root
                RETURNING journal_root
                """, [root], ct).ConfigureAwait(false);
            if (bound is not Guid id || id != root)
                throw new InvalidOperationException("ITI source WAL is on another journal root. Mount the original journal before ownership handover.");
            return true;
        }, token);

    public Task<bool> BridgeEpochAsync(string stream, Guid prior, long ordinal, long sequence, Guid next, CancellationToken token)
        => ExecuteAsync(async (transaction, ct) =>
        {
            if (await transaction.ScalarAsync("""
                SELECT to_epoch FROM futures_iti_epoch_bridge WHERE stream=$1 AND from_epoch=$2
                    AND last_ordinal=$3 AND input_id=$4
                """, [stream, prior, ordinal, sequence], ct).ConfigureAwait(false) is Guid existing)
            {
                if (existing != next) throw new InvalidOperationException("ITI epoch was already handed to a different successor.");
                return true;
            }
            var result = await transaction.ScalarAsync("""
                INSERT INTO futures_iti_epoch_bridge(stream,from_epoch,to_epoch,last_ordinal,input_id)
                SELECT stream,epoch,$5,last_ordinal,input_id FROM futures_iti_epoch_seal
                WHERE stream=$1 AND epoch=$2 AND last_ordinal=$3 AND input_id=$4
                ON CONFLICT(stream,from_epoch) DO UPDATE SET to_epoch=futures_iti_epoch_bridge.to_epoch
                WHERE futures_iti_epoch_bridge.to_epoch=EXCLUDED.to_epoch
                    AND futures_iti_epoch_bridge.input_id=EXCLUDED.input_id
                RETURNING to_epoch
                """, [stream, prior, ordinal, sequence, next], ct).ConfigureAwait(false);
            if (result is not Guid accepted || accepted != next)
                throw new InvalidOperationException("ITI epoch handover has no complete sealed predecessor.");
            return true;
        }, token);

    public Task<IReadOnlyList<RetiredEpoch>> ReadRetiredEpochsAsync(CancellationToken token)
        => ExecuteAsync(async (transaction, ct) => await transaction.QueryAsync(
            "SELECT stream,from_epoch,last_ordinal FROM futures_iti_epoch_bridge", [],
            reader => new RetiredEpoch(reader.GetString(0), reader.GetGuid(1), reader.GetInt64(2)), ct).ConfigureAwait(false), token);

    public Task<bool> SealAsync(string stream, Guid epoch, long ordinal, long sequence, long version, CancellationToken token)
        => ExecuteAsync(async (transaction, ct) =>
        {
            await CheckpointAsync(transaction, stream, sequence, version, ct).ConfigureAwait(false);
            _ = await transaction.ExecuteAsync("""
                INSERT INTO futures_iti_epoch_seal(stream,epoch,last_ordinal,input_id) VALUES ($1,$2,$3,$4)
                ON CONFLICT(stream,epoch) DO UPDATE SET last_ordinal=EXCLUDED.last_ordinal,input_id=EXCLUDED.input_id
                """, [stream, epoch, ordinal, sequence], ct).ConfigureAwait(false);
            return true;
        }, token);

    async Task PulseAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(lifetime.Token).ConfigureAwait(false))
            {
                await gate.WaitAsync(lifetime.Token).ConfigureAwait(false);
                try
                {
                    await using var command = new NpgsqlCommand("SELECT 1", connection) { CommandTimeout = 2 };
                    _ = await command.ExecuteScalarAsync(lifetime.Token).ConfigureAwait(false);
                    Interlocked.Exchange(ref lastProof, System.Diagnostics.Stopwatch.GetTimestamp());
                }
                finally { gate.Release(); }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch { Interlocked.Exchange(ref failed, 1); }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await lifetime.CancelAsync().ConfigureAwait(false);
        await heartbeat.ConfigureAwait(false);
        await gate.WaitAsync().ConfigureAwait(false);
        try { await connection.DisposeAsync().ConfigureAwait(false); }
        finally { gate.Release(); gate.Dispose(); lifetime.Dispose(); }
    }

    public sealed record Input(Guid CommandId, string Stream, string Dataset, int PublisherId, long InstrumentId,
        Guid Epoch, long Ordinal, long MarketTicks, byte[] Payload);
    public sealed record StoredInput(long Id, string Stream, byte[] Payload);
    public sealed record RetiredEpoch(string Stream, Guid Epoch, long LastOrdinal);
}
