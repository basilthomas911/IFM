using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using TomasAI.IFM.Application.Api.Server.Benchmarks;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Storage;
using System.Diagnostics;
using System.Text.Json;

namespace TomasAI.IFM.Framework.Caching.Benchmarks;

/// <summary>Compares the retained Redis hit algorithm with production typed stream resolution.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig))]
public class RedisRemovalIdentityBenchmarks
{
    readonly RedisRemovalFixture fixture = new();
    [GlobalSetup] public Task Setup() => fixture.StartAsync();
    [Benchmark(Baseline = true)] public ValueTask<long> RedisBefore() => fixture.LegacyAsync("warm");
    [Benchmark] public Task<long> LocalAfter() => fixture.Events.GetEventStreamIdAsync("warm");
    [GlobalCleanup] public ValueTask Cleanup() => fixture.DisposeAsync();
}

/// <summary>Measures the exact extra recovery mirror cost around an already retrieved PostgreSQL state.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig))]
public class RedisRemovalProjectorMirrorBenchmarks
{
    readonly RedisRemovalFixture fixture = new();
    readonly EventProjectorStateReadModel state = new(100, "actor", "projector", true, 1,
        EventProjectorOutcomeType.Retrying, EventProjectorStageType.PublishProcessingEvent);
    [GlobalSetup] public Task Setup() => fixture.StartAsync();
    [Benchmark(Baseline = true)] public long RedisMirrorBefore() { fixture.Mirror(state); return state.EventId; }
    [Benchmark] public long NoMirrorAfter() => state.EventId;
    [GlobalCleanup] public ValueTask Cleanup() => fixture.DisposeAsync();
}

/// <summary>Owns disposable stores and a baseline copied from the pre-removal cache algorithm.</summary>
internal sealed partial class RedisRemovalFixture : IAsyncDisposable
{
    readonly IsolatedIntegrationInfrastructure infrastructure = new("redisremovalbench", includeCql: false, includeRedis: true);
    readonly SystemTextJsonSerializer json = new();
    ConnectionMultiplexer connection = null!;
    IDatabase redis = null!;
    EventSourceActorDbContext raw = null!;
    public EventSourceActorDbContext Events { get; private set; } = null!;

    public async Task StartAsync()
    {
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            infrastructure.PostgresConnectionString, "System.Data.Postgres");
        await new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var repositories = new Dictionary<Type, object>();
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        Events = new(settings, factory, NullLogger<DbProvider>.Instance);
        repositories.Add(typeof(IObjectRepository<EventSourceActorDbContext>), Events);
        raw = new(settings, factory, NullLogger<DbProvider>.Instance);
        connection = await ConnectionMultiplexer.ConnectAsync(infrastructure.RedisConnectionString);
        redis = connection.GetDatabase();
        _ = await LegacyAsync("warm");
        _ = await Events.GetEventStreamIdAsync("warm");
    }

    // Original EventStreamIdCacheModel/RedisCache behavior: EXISTS, GET, JSON; on miss resolve
    // PostgreSQL, serialize and SET asynchronously. No production assembly depends on this copy.
    public async ValueTask<long> LegacyAsync(string stream)
    {
        var key = "EventStreamId:" + stream;
        string? value = redis.KeyExists(key) ? redis.StringGet(key).ToString() : null;
        if (value is null)
        {
            var id = await ResolvePostgresAsync(stream);
            value = json.Serialize(new EventStreamIdReadModel(id, stream));
            await redis.StringSetAsync(key, value);
        }
        return json.Deserialize<EventStreamIdReadModel>(value).EventStreamId;
    }

    async Task<long> ResolvePostgresAsync(string stream)
    {
        var existing = await raw.Use(nameof(EventSourceDbSql.GetEventStreamId), EventSourceDbSql.GetEventStreamId)
            .SetParameters(new StreamBinding(stream)).ExecuteSingleAsync<EventStreamIdReadModel>(
                row => new(row.GetLong(0), row.GetString(1)));
        if (existing is not null) return existing.EventStreamId;
        return await raw.Use(nameof(EventSourceDbSql.InsertEventStreamId), EventSourceDbSql.InsertEventStreamId)
            .SetParameters(new StreamBinding(stream)).ExecuteScalarAsync(row => row.GetLong(0));
    }

    public void Mirror(EventProjectorStateReadModel state) => redis.StringSet(
        "EventProjectorState:" + state.EventId + ":" + state.ProjectorName, json.Serialize(state));

    public async Task MeasureColdAndConcurrentAsync()
    {
        var results = new List<object>();
        foreach (var before in new[] { true, false })
        {
            var cold = new List<double>();
            // Existing durable metadata with empty caches: both paths include PostgreSQL stream resolution.
            for (var sample = 0; sample < 200; sample++)
            {
                var stream = "cold-" + before + "-" + sample;
                _ = await ResolvePostgresAsync(stream);
                var started = Stopwatch.GetTimestamp();
                _ = before ? await LegacyAsync(stream) : await Events.GetEventStreamIdAsync(stream);
                cold.Add(Stopwatch.GetElapsedTime(started).TotalMicroseconds);
            }
            cold.Sort();
            foreach (var concurrency in new[] { 1, 8, 32 })
            {
                var durations = new List<double>();
                for (var sample = 0; sample < 40; sample++)
                {
                    var stream = "burst-" + before + "-" + concurrency + "-" + sample;
                    _ = await ResolvePostgresAsync(stream);
                    var started = Stopwatch.GetTimestamp();
                    var tasks = Enumerable.Range(0, concurrency).Select(_ => before
                        ? LegacyAsync(stream).AsTask() : Events.GetEventStreamIdAsync(stream)).ToArray();
                    var ids = await Task.WhenAll(tasks);
                    if (ids.Any(id => id != ids[0])) throw new InvalidOperationException("Identity mismatch.");
                    durations.Add(Stopwatch.GetElapsedTime(started).TotalMicroseconds);
                }
                durations.Sort();
                results.Add(new { Path = before ? "RedisBefore" : "LocalAfter", Workload = "SameKeyColdBurst", Concurrency = concurrency,
                    Samples = durations.Count, MeanUs = durations.Average(), P50Us = durations[durations.Count / 2], P95Us = durations[(int)(durations.Count * .95)] });
            }
            results.Add(new { Path = before ? "RedisBefore" : "LocalAfter", Workload = "ColdExistingStream", Samples = cold.Count,
                MeanUs = cold.Average(), P50Us = cold[cold.Count / 2], P95Us = cold[(int)(cold.Count * .95)], P99Us = cold[(int)(cold.Count * .99)] });
        }
        Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null) await connection.DisposeAsync();
        if (raw is not null) await ((IAsyncDisposable)raw).DisposeAsync();
        if (Events is not null) await ((IAsyncDisposable)Events).DisposeAsync();
        await infrastructure.DisposeAsync();
    }

    readonly record struct StreamBinding(string Stream) : IBindValue
    {
        public object Bind() => BindValues.Values(BindValues.Text(Stream));
    }
}
