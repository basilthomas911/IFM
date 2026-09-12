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
/// Compares legacy serial mailbox dispatch with the production keyed actor scheduling behavior for independent
/// single-event streams. Both cases use the same appender, payload, stream count, and expected-version policy.
/// </summary>
[MemoryDiagnoser]
[InProcess]
[WarmupCount(1)]
[IterationCount(5)]
[InvocationCount(1)]
public class EventLogConcurrentAppenderBenchmarks
{
    const string ConnectionVariable = "IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION";
    const int CommandCount = 64;
    string _connectionString = string.Empty;
    (string Name, long Id)[] _serialStreams = [];
    (string Name, long Id)[] _parallelStreams = [];
    int _eventNameId;
    long _serialStreamVersion;
    long _parallelStreamVersion;
    IEventLogAppender _appender = null!;

    [Params(EventLogWriteMode.Sequential, EventLogWriteMode.BinaryCopy)]
    public EventLogWriteMode WriteMode { get; set; }

    [Params(false, true)]
    public bool UseLz4Compression { get; set; }

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
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
        await connection.OpenAsync();
        _serialStreams = await CreateStreamsAsync(connection, "Serial");
        _parallelStreams = await CreateStreamsAsync(connection, "Parallel");
        var eventType = typeof(UnknownEvent);
        await using (var command = new NpgsqlCommand(EventSourceDbSql.InsertEventNameId, connection))
        {
            command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = eventType.Name });
            command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = eventType.AssemblyQualifiedName! });
            _eventNameId = Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        var options = new EventLogPersistenceOptions
        {
            WriteMode = WriteMode,
            UseLz4Compression = UseLz4Compression,
            MaximumEventsPerBatch = 256,
            MaximumBatchBytes = 32 * 1024 * 1024,
            MaximumOldestRequestDelay = TimeSpan.FromMilliseconds(1)
        };
        _appender = WriteMode == EventLogWriteMode.Sequential
            ? new SequentialEventLogAppender(_connectionString, UseLz4Compression, options)
            : new BinaryCopyEventLogAppender(_connectionString, UseLz4Compression, options);
        await WriteSerialCommands();
        await WriteStreamParallelCommands();
    }

    [Benchmark(Baseline = true, Description = "Before: 64 serial independent-stream commands")]
    public async Task<int> WriteSerialCommands()
    {
        var expected = _serialStreamVersion;
        for (var index = 0; index < _serialStreams.Length; index++)
        {
            await AppendOneAsync(_serialStreams[index], expected, index);
        }

        _serialStreamVersion++;
        return _serialStreams.Length;
    }

    [Benchmark(Description = "After: 64 keyed-parallel independent-stream commands")]
    public async Task<int> WriteStreamParallelCommands()
    {
        var expected = _parallelStreamVersion;
        var tasks = new Task<EventLogAppendResult>[_parallelStreams.Length];
        for (var index = 0; index < _parallelStreams.Length; index++)
            tasks[index] = AppendOneAsync(_parallelStreams[index], expected, index).AsTask();

        await Task.WhenAll(tasks);
        _parallelStreamVersion++;
        return tasks.Length;
    }

    ValueTask<EventLogAppendResult> AppendOneAsync((string Name, long Id) stream, long expected, int index)
    {
        var now = DateTime.UtcNow;
        var @event = new UnknownEvent(default, Guid.NewGuid(), new ActorEntityId("benchmark"), 0, Guid.NewGuid(),
            "benchmark", "EventLogConcurrentAppenderBenchmarks", now, index, 1,
            "benchmark", new string('x', 1024), now);
        return _appender.AppendAsync(new EventLogAppendRequest(
            stream.Name,
            stream.Id,
            Guid.NewGuid(),
            [new EventLogAppendEntry(_eventNameId, @event)],
            expected,
            now));
    }

    async Task<(string Name, long Id)[]> CreateStreamsAsync(NpgsqlConnection connection, string dispatchMode)
    {
        var streams = new (string, long)[CommandCount];
        for (var index = 0; index < streams.Length; index++)
        {
            var name = $"EventLogConcurrentBenchmark.{dispatchMode}.{WriteMode}.{UseLz4Compression}.{index}.{Guid.NewGuid():N}";
            await using var command = new NpgsqlCommand(EventSourceDbSql.InsertEventStreamId, connection);
            command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = name });
            streams[index] = (name, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }

        return streams;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_appender is not null) await _appender.DisposeAsync();
        if (_serialStreams.Length == 0 && _parallelStreams.Length == 0) return;
        var ids = _serialStreams.Concat(_parallelStreams).Select(static stream => stream.Id).ToArray();
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var events = new NpgsqlCommand("DELETE FROM event_log WHERE EventStreamId = ANY($1);", connection, transaction))
        {
            events.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Bigint, Value = ids });
            await events.ExecuteNonQueryAsync();
        }
        await using (var streams = new NpgsqlCommand("DELETE FROM event_stream_id WHERE EventStreamId = ANY($1);", connection, transaction))
        {
            streams.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Bigint, Value = ids });
            await streams.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }
}
