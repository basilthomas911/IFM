using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Newtonsoft.Json;
using Npgsql;
using TomasAI.IFM.Application.Storage.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class CommandAuditSerializationBenchmarks
{
    readonly CommandAuditMessagePackCodec _codec = new();
    BenchmarkCommand _command = null!;

    [GlobalSetup]
    public void Setup() => _command = BenchmarkCommand.Create(Guid.Parse("6aa473be-3fd2-4fc2-8ef4-052f472195c0"), 123.45m);

    [Benchmark(Baseline = true)]
    public string NewtonsoftJson() => JsonConvert.SerializeObject(_command);

    [Benchmark]
    public CommandAuditPayload UncompressedMessagePack() => _codec.Serialize(_command);
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 1, iterationCount: 3)]
public class CommandAuditPostgresBenchmarks
{
    string _connectionString = null!;
    string _rawConnectionString = null!;
    PostgresCommandAuditWriter _writer = null!;
    long _sequence;
    long _runSeed;

    [Params(CommandAuditWriteMode.SequentialMessagePack, CommandAuditWriteMode.WindowedMessagePack)]
    public CommandAuditWriteMode Mode { get; set; }

    [Params(1, 16, 64)]
    public int ConcurrentCommands { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _rawConnectionString = Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")
            ?? throw new InvalidOperationException("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION is required.");
        var builder = new NpgsqlConnectionStringBuilder(_rawConnectionString)
        {
            Username = string.Empty,
            Password = string.Empty
        };
        _connectionString = builder.ConnectionString;
        _runSeed = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
        _writer = new PostgresCommandAuditWriter(_connectionString, new CommandAuditPersistenceOptions
        {
            WriteMode = Mode,
            MaximumCommandsPerBatch = 64,
            MaximumOldestRequestDelay = TimeSpan.FromMilliseconds(1)
        });
    }

    [Benchmark]
    public async Task ReserveWindow()
    {
        var operations = new ValueTask<CommandAuditWriteResult>[ConcurrentCommands];
        for (var index = 0; index < operations.Length; index++)
        {
            var id = DeterministicGuid(Interlocked.Increment(ref _sequence) ^ _runSeed);
            var command = BenchmarkCommand.Create(id, index);
            operations[index] = _writer.ReserveAsync(
                CommandAuditEnvelope.Create(command, new CommandAuditMessagePackCodec()));
        }
        for (var index = 0; index < operations.Length; index++)
            _ = await operations[index].ConfigureAwait(false);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_writer is not null) await _writer.DisposeAsync();
        await using var connection = new NpgsqlConnection(_rawConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM command_log WHERE commandname='BenchmarkCommand'", connection);
        await command.ExecuteNonQueryAsync();
    }

    static Guid DeterministicGuid(long value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        BitConverter.TryWriteBytes(bytes[8..], ~value);
        return new Guid(bytes);
    }
}

public sealed record BenchmarkCommand : ICommand
{
    public required ActorSubject Subject { get; init; }
    public string CommandName => nameof(BenchmarkCommand);
    public BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext;
    public Guid CommandId { get; init; }
    public string StreamId => Subject.StreamId;
    public string EventSource => "CommandAuditBenchmark";
    public int ErrorCode => 1;
    public decimal Price { get; init; }
    public int Quantity { get; init; } = 4;
    public string Symbol { get; init; } = "ESZ6 C6000";

    public static BenchmarkCommand Create(Guid id, decimal price) => new()
    {
        Subject = new ActorSubject(ActorType.Command, "OptionTradeBenchmark", "ChangeLeg", "42"),
        CommandId = id,
        Price = price
    };
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 1, iterationCount: 3)]
public class AtomicCommandEventPostgresBenchmarks
{
    string _rawConnectionString = null!;
    string _connectionString = null!;
    BinaryCopyEventLogAppender _appender = null!;
    readonly CommandAuditMessagePackCodec _auditCodec = new();
    string _entity = null!;
    string _stream = null!;
    long _streamId;
    int _eventNameId;
    long _streamVersion;
    long _commandSequence;

    [Params(1, 64)]
    public int PhysicalBatchEvents { get; set; }

    [Params(false, true)]
    public bool UseLz4 { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _rawConnectionString = Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")
            ?? throw new InvalidOperationException("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION is required.");
        var builder = new NpgsqlConnectionStringBuilder(_rawConnectionString)
        {
            Username = string.Empty,
            Password = string.Empty
        };
        _connectionString = builder.ConnectionString;
        _entity = Guid.NewGuid().ToString("N");
        _stream = new ActorSubject(ActorType.Command, "OptionTradeBenchmark", "ChangeLeg", _entity).StreamId;
        await using var connection = new NpgsqlConnection(_rawConnectionString);
        await connection.OpenAsync();
        await using (var command = new NpgsqlCommand(
            "INSERT INTO event_stream_id(eventstream) VALUES ($1) RETURNING eventstreamid", connection))
        {
            command.Parameters.AddWithValue(_stream);
            _streamId = Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        var eventType = typeof(FuturesRsiSignalGeneratedEvent);
        await using (var command = new NpgsqlCommand("""
            INSERT INTO event_name_id(eventname, eventtypename) VALUES ($1, $2)
            ON CONFLICT (eventname, eventtypename) DO UPDATE SET eventname=EXCLUDED.eventname
            RETURNING eventnameid
            """, connection))
        {
            command.Parameters.AddWithValue(eventType.Name);
            command.Parameters.AddWithValue(eventType.AssemblyQualifiedName!);
            _eventNameId = Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        _appender = new BinaryCopyEventLogAppender(_connectionString, UseLz4, new EventLogPersistenceOptions
        {
            WriteMode = EventLogWriteMode.BinaryCopy,
            UseLz4Compression = UseLz4,
            MaximumEventsPerBatch = PhysicalBatchEvents,
            MaximumOldestRequestDelay = TimeSpan.FromMilliseconds(1)
        });
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public async Task CommitSixtyFourSameStreamCommands()
    {
        var start = Interlocked.Add(ref _streamVersion, 64) - 64;
        var operations = new ValueTask<EventLogAppendResult>[64];
        for (var index = 0; index < operations.Length; index++)
        {
            var sequence = Interlocked.Increment(ref _commandSequence);
            var id = DeterministicGuid(sequence);
            var command = BenchmarkCommand.Create(id, sequence) with
            {
                Subject = new ActorSubject(ActorType.Command, "OptionTradeBenchmark", "ChangeLeg", _entity)
            };
            var domainEvent = new FuturesRsiSignalGeneratedEvent
            {
                CommandId = id,
                EntityId = new FuturesRsiSignalEntityId("ES", new DateOnly(2026, 9, 12), TimeFrameType.Daily, 14),
                CreatedOn = DateTime.UtcNow,
                CreatedBy = "atomic-window-benchmark"
            };
            operations[index] = _appender.AppendAsync(new EventLogAppendRequest(
                _stream, _streamId, id, [new EventLogAppendEntry(_eventNameId, domainEvent)],
                start + index, DateTime.UtcNow, CommandAuditEnvelope.Create(command, _auditCodec)));
        }
        for (var index = 0; index < operations.Length; index++)
            _ = await operations[index].ConfigureAwait(false);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_appender is not null) await _appender.DisposeAsync();
        await using var connection = new NpgsqlConnection(_rawConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var events = new NpgsqlCommand("DELETE FROM event_log WHERE eventstreamid=$1", connection, transaction))
        {
            events.Parameters.AddWithValue(_streamId);
            await events.ExecuteNonQueryAsync();
        }
        await using (var stream = new NpgsqlCommand("DELETE FROM event_stream_id WHERE eventstreamid=$1", connection, transaction))
        {
            stream.Parameters.AddWithValue(_streamId);
            await stream.ExecuteNonQueryAsync();
        }
        await using (var audits = new NpgsqlCommand(
            "DELETE FROM command_log WHERE commandname='BenchmarkCommand' AND streamid=$1", connection, transaction))
        {
            audits.Parameters.AddWithValue(_stream);
            await audits.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    static Guid DeterministicGuid(long value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        BitConverter.TryWriteBytes(bytes[8..], ~value);
        return new Guid(bytes);
    }
}
