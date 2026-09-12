using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>
/// Measures sustained production-format throughput using one event, one command, and one committed PostgreSQL
/// transaction per sequential append. Each configured transaction count is executed in full exactly once.
/// </summary>
[MemoryDiagnoser]
[WarmupCount(0)]
[IterationCount(1)]
[InvocationCount(1)]
public class EventLogSequentialTransactionRateBenchmarks
{
    const string ConnectionVariable = "IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION";
    const string CountsVariable = "IFM_EVENT_LOG_TRANSACTION_COUNTS";
    string _connectionString = string.Empty;
    string _stream = string.Empty;
    long _streamId;
    long _streamVersion;
    int _eventNameId;
    IEventLogAppender _appender = null!;
    bool _jittingInvocation = true;

    [ParamsSource(nameof(TransactionCounts))]
    public int TransactionCount { get; set; }

    public IEnumerable<int> TransactionCounts
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(CountsVariable);
            return string.IsNullOrWhiteSpace(configured)
                ? [1_000, 10_000, 100_000]
                : configured.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse)
                    .Where(static count => count > 0)
                    .Distinct()
                    .Order();
        }
    }

    [GlobalSetup]
    public async Task Setup()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
        _connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)
            ?? throw new InvalidOperationException($"Set {ConnectionVariable} for a dedicated PostgreSQL test database.");
        var settings = new DbConnectionSettings().Add(
            EventSourceActorDbContext.EventSourceActorDbConnection,
            _connectionString,
            "System.Data.Postgres");
        await new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        _stream = $"EventLogSequentialRate.{TransactionCount}.{Guid.NewGuid():N}";
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
        await connection.OpenAsync();
        await using (var streamCommand = new NpgsqlCommand(EventSourceDbSql.InsertEventStreamId, connection))
        {
            streamCommand.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = _stream });
            _streamId = Convert.ToInt64(await streamCommand.ExecuteScalarAsync());
        }
        var eventType = typeof(UnknownEvent);
        await using (var nameCommand = new NpgsqlCommand(EventSourceDbSql.InsertEventNameId, connection))
        {
            nameCommand.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = eventType.Name });
            nameCommand.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = eventType.AssemblyQualifiedName! });
            _eventNameId = Convert.ToInt32(await nameCommand.ExecuteScalarAsync());
        }
        _appender = new SequentialEventLogAppender(_connectionString, useLz4Compression: true,
            new EventLogPersistenceOptions
            {
                WriteMode = EventLogWriteMode.Sequential,
                UseLz4Compression = true
            });

        // Initialize Npgsql pooling, codec metadata, and the prepared event-name registry outside the measurement.
        await AppendOneAsync();
    }

    [Benchmark(Description = "Sequential one-event committed transactions")]
    public async Task<int> WriteTransactions()
    {
        // BenchmarkDotNet invokes the workload once to JIT its delegate. A full sustained run during that probe would
        // double the database mutation and wall time, so the probe performs one representative transaction.
        if (_jittingInvocation)
        {
            _jittingInvocation = false;
            await AppendOneAsync();
            return 1;
        }
        for (var index = 0; index < TransactionCount; index++)
            await AppendOneAsync();
        return TransactionCount;
    }

    async Task AppendOneAsync()
    {
        var @event = new UnknownEvent(default, Guid.NewGuid(), new ActorEntityId("benchmark"), 0, Guid.NewGuid(),
            "benchmark", "EventLogSequentialTransactionRateBenchmarks", DateTime.UtcNow, 1, 1,
            "benchmark", "sequential-transaction-rate", DateTime.UtcNow);
        await _appender.AppendAsync(new EventLogAppendRequest(
            _stream,
            _streamId,
            Guid.NewGuid(),
            [new EventLogAppendEntry(_eventNameId, @event)],
            _streamVersion,
            DateTime.UtcNow));
        _streamVersion++;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_appender is not null) await _appender.DisposeAsync();
        if (_streamId == 0) return;
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var events = new NpgsqlCommand("DELETE FROM event_log WHERE EventStreamId=$1;", connection, transaction))
        {
            events.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bigint, Value = _streamId });
            await events.ExecuteNonQueryAsync();
        }
        await using (var stream = new NpgsqlCommand("DELETE FROM event_stream_id WHERE EventStreamId=$1;", connection, transaction))
        {
            stream.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bigint, Value = _streamId });
            await stream.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }
}
