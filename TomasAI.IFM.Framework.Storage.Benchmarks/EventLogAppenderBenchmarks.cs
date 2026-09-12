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

/// <summary>Measures durable event-log writes and replay reads for both appenders with LZ4 enabled and disabled.</summary>
[MemoryDiagnoser]
[InProcess]
[WarmupCount(1)]
[IterationCount(5)]
[InvocationCount(1)]
public class EventLogAppenderBenchmarks
{
    const string ConnectionVariable = "IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION";
    string _connectionString = string.Empty;
    string _stream = string.Empty;
    long _streamId;
    long _streamVersion;
    int _eventNameId;
    IEventLogAppender _appender = null!;

    [Params(EventLogWriteMode.Sequential, EventLogWriteMode.BinaryCopy)]
    public EventLogWriteMode WriteMode { get; set; }

    [Params(false, true)]
    public bool UseLz4Compression { get; set; }

    [Params(1, 64, 256)]
    public int EventCount { get; set; }

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
        _stream = $"EventLogAppenderBenchmark.{WriteMode}.{UseLz4Compression}.{EventCount}.{Guid.NewGuid():N}";
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
        var options = new EventLogPersistenceOptions
        {
            WriteMode = WriteMode,
            UseLz4Compression = UseLz4Compression,
            MaximumEventsPerBatch = Math.Max(256, EventCount),
            MaximumBatchBytes = 32 * 1024 * 1024,
            MaximumOldestRequestDelay = TimeSpan.FromMilliseconds(1)
        };
        _appender = WriteMode == EventLogWriteMode.Sequential
            ? new SequentialEventLogAppender(_connectionString, UseLz4Compression, options)
            : new BinaryCopyEventLogAppender(_connectionString, UseLz4Compression, options);
        await AppendCoreAsync();
    }

    [Benchmark(Description = "Durable append")]
    public Task<EventLogAppendResult> Write() => AppendCoreAsync();

    [Benchmark(Description = "Read and deserialize latest batch")]
    public async Task<int> Read()
    {
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT EventVersion, EventPayload
            FROM event_log
            WHERE EventStreamId=$1
            ORDER BY StreamVersion DESC
            LIMIT $2;
            """, connection);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bigint, Value = _streamId });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Integer, Value = EventCount });
        var read = 0;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            _ = EventLogMessagePackCodec.Shared.Deserialize(
                typeof(UnknownEvent).AssemblyQualifiedName!, reader.GetInt64(0), (byte[])reader.GetValue(1));
            read++;
        }
        return read;
    }

    async Task<EventLogAppendResult> AppendCoreAsync()
    {
        var events = new EventLogAppendEntry[EventCount];
        for (var index = 0; index < events.Length; index++)
        {
            events[index] = new EventLogAppendEntry(_eventNameId,
                new UnknownEvent(default, Guid.NewGuid(), new ActorEntityId("benchmark"), 0, Guid.NewGuid(),
                    "benchmark", "EventLogAppenderBenchmarks", DateTime.UtcNow, index, 1,
                    "benchmark", new string('x', 1024), DateTime.UtcNow));
        }
        var expected = _streamVersion;
        var result = await _appender.AppendAsync(new EventLogAppendRequest(
            _stream, _streamId, Guid.NewGuid(), events, expected, DateTime.UtcNow));
        _streamVersion += EventCount;
        return result;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_appender is not null) await _appender.DisposeAsync();
        if (string.IsNullOrWhiteSpace(_connectionString) || _streamId == 0) return;
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
