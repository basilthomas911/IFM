using System.Data;
using System.Threading.Channels;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage.Postgres;

namespace TomasAI.IFM.Application.Storage.CommandAudit;

/// <summary>Single-consumer, event-driven PostgreSQL command-audit writer.</summary>
internal sealed class PostgresCommandAuditWriter : ICommandAuditWriter
{
    readonly string _connectionString;
    readonly CommandAuditPersistenceOptions _options;
    readonly Channel<PendingAudit> _queue;
    readonly CancellationTokenSource _lifetime = new();
    readonly Task _consumer;
    NpgsqlConnection? _connection;
    int _disposed;

    public PostgresCommandAuditWriter(string connectionString, CommandAuditPersistenceOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        _queue = Channel.CreateBounded<PendingAudit>(new BoundedChannelOptions(_options.QueueCommandCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _consumer = ConsumeAsync(_lifetime.Token);
    }

    public async ValueTask<CommandAuditWriteResult> ReserveAsync(
        CommandAuditEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.PayloadBytes <= 0 || envelope.PayloadBytes > _options.MaximumCommandPayloadBytes)
            throw new ArgumentException("Command audit payload is outside its configured size limit.", nameof(envelope));

        var pending = new PendingAudit(envelope);
        await _queue.Writer.WriteAsync(pending, cancellationToken).ConfigureAwait(false);
        return await pending.Completion.Task.ConfigureAwait(false);
    }

    async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        PendingAudit? carry = null;
        try
        {
            while (carry is not null || await _queue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var first = carry ?? (_queue.Reader.TryRead(out var queued) ? queued : null);
                carry = null;
                if (first is null) continue;

                var batch = new List<PendingAudit> { first };
                var bytes = first.Envelope.PayloadBytes;
                var maximumCommands = _options.WriteMode == CommandAuditWriteMode.WindowedMessagePack
                    ? _options.MaximumCommandsPerBatch
                    : 1;
                if (maximumCommands > 1)
                {
                    using var deadlineCancellation = new CancellationTokenSource();
                    var deadline = Task.Delay(_options.MaximumOldestRequestDelay, deadlineCancellation.Token);
                    while (batch.Count < maximumCommands && bytes < _options.MaximumBatchBytes)
                    {
                        if (_queue.Reader.TryRead(out var next))
                        {
                            if (bytes + next.Envelope.PayloadBytes > _options.MaximumBatchBytes)
                            {
                                carry = next;
                                break;
                            }
                            batch.Add(next);
                            bytes += next.Envelope.PayloadBytes;
                            continue;
                        }

                        var available = _queue.Reader.WaitToReadAsync(cancellationToken).AsTask();
                        var completed = await Task.WhenAny(available, deadline).ConfigureAwait(false);
                        if (completed == deadline) break;
                        if (!await available.ConfigureAwait(false)) break;
                    }
                    deadlineCancellation.Cancel();
                }

                await PersistAsync(batch, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var failure = new OperationCanceledException("Command audit writer stopped before persistence completed.");
            if (carry is not null) carry.Completion.TrySetException(failure);
            while (_queue.Reader.TryRead(out var pending)) pending.Completion.TrySetException(failure);
        }
        catch (Exception exception)
        {
            if (carry is not null) carry.Completion.TrySetException(exception);
            while (_queue.Reader.TryRead(out var pending)) pending.Completion.TrySetException(exception);
        }
    }

    async Task PersistAsync(IReadOnlyList<PendingAudit> batch, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                var results = await CommandAuditPostgres.ReserveAsync(
                    connection, transaction, batch.Select(static item => item.Envelope).ToArray(), cancellationToken)
                    .ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                for (var index = 0; index < batch.Count; index++)
                    batch[index].Completion.TrySetResult(results[index]);
            }
            catch
            {
                try { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
                throw;
            }
        }
        catch (Exception exception)
        {
            if (_connection is not { FullState: ConnectionState.Open })
            {
                if (_connection is not null) await _connection.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }
            foreach (var pending in batch) pending.Completion.TrySetException(exception);
        }
    }

    async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { FullState: ConnectionState.Open }) return _connection;
        if (_connection is not null) await _connection.DisposeAsync().ConfigureAwait(false);
        _connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
        await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return _connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _queue.Writer.TryComplete();
        var completed = await Task.WhenAny(_consumer, Task.Delay(_options.ShutdownDrainTimeout)).ConfigureAwait(false);
        if (completed != _consumer)
        {
            _lifetime.Cancel();
            await _consumer.ConfigureAwait(false);
        }
        else
        {
            await _consumer.ConfigureAwait(false);
        }
        if (_connection is not null) await _connection.DisposeAsync().ConfigureAwait(false);
        _lifetime.Dispose();
    }

    sealed class PendingAudit(CommandAuditEnvelope envelope)
    {
        public CommandAuditEnvelope Envelope { get; } = envelope;
        public TaskCompletionSource<CommandAuditWriteResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
