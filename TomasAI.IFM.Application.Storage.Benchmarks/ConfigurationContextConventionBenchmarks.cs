using TomasAI.IFM.Application.Strategy.StrategyCatalog;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using System.Reflection;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures exact catalog publication and read qualification on the real application path.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
public class ConfigurationContextConventionBenchmarks
{
    delegate Task Publish(CatalogKey key, string hash, DateTime at, string principal, CancellationToken token);
    delegate Task<StrategyCatalogSnapshot> Read(CatalogKey key, DateTime at, CancellationToken token);
    delegate Task PublishOwned(IConfigurationDbContext db, CatalogKey key, string hash, DateTime at, string principal, CancellationToken token);
    delegate Task<StrategyCatalogSnapshot> ReadOwned(IConfigurationDbReadContext db, CatalogKey key, DateTime at, CancellationToken token);
    ConfigurationDbContext database = null!;
    Publish publish = null!;
    Read read = null!;
    CatalogKey published = null!;
    static readonly DateTime At = new(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc);

    [Params(false, true)] public bool ColdConnection { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var raw = Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")
            ?? throw new InvalidOperationException("Provision the disposable qualification database first.");
        var builder = new NpgsqlConnectionStringBuilder(raw);
        if (builder.Host != "127.0.0.1" || builder.Port != 58231 || builder.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Only the disposable qualification database is allowed.");
        await using (var guard = new NpgsqlConnection(raw))
        {
            await guard.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT current_setting('fsync'),current_setting('synchronous_commit'),current_setting('full_page_writes')", guard);
            await using var row = await command.ExecuteReaderAsync();
            await row.ReadAsync();
            if (Enumerable.Range(0, 3).Any(index => row.GetString(index) != "on"))
                throw new InvalidOperationException("Qualification requires full WAL durability.");
        }
        builder.Pooling = !ColdConnection;
        builder.Username = "";
        builder.Password = "";
        var settings = new DbConnectionSettings().Add(ConfigurationDbContext.ConfigurationDbConnection,
            builder.ConnectionString, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new ConfigurationSchemaDb(settings, logger).CreateAllAsync();
        var factory = new DbContextFactory(new DbContextResolver(_ => database));
        database = new(settings, factory, logger);
        if (database.GetType().GetMethod("PublishStrategyCatalogAsync") is { } legacy)
        {
            publish = legacy.CreateDelegate<Publish>(database);
            read = database.GetType().GetMethod("GetPublishedStrategyDeploymentAsync")!.CreateDelegate<Read>(database);
        }
        else
        {
            var type = Type.GetType("TomasAI.IFM.Application.Strategy.StrategyCatalog.StrategyCatalogQualification, TomasAI.IFM.Application.Strategy", true)!;
            var owner = Activator.CreateInstance(type, [null, null])!;
            var publishOwned = type.GetMethod("PublishAsync", BindingFlags.Public | BindingFlags.Instance)!.CreateDelegate<PublishOwned>(owner);
            var readOwned = type.GetMethod("ReadPublishedAsync", BindingFlags.Public | BindingFlags.Instance)!.CreateDelegate<ReadOwned>(owner);
            publish = (key, hash, at, principal, token) => publishOwned(database, key, hash, at, principal, token);
            read = (key, at, token) => readOwned(database, key, at, token);
        }
        published = await PublishNewFamily().ConfigureAwait(false);
        var current = await ReadPublishedFamily().ConfigureAwait(false);
        if (current.Deployment != published || current.Definitions.Length != 1)
            throw new InvalidOperationException("Published catalog qualification fixture returned a different version.");
    }

    [Benchmark] public Task<StrategyCatalogSnapshot> ReadPublishedFamily() => read(published, At, CancellationToken.None);

    [Benchmark] public async Task<CatalogKey> PublishNewFamily()
    {
        var row = StrategyCatalogExamples.New(StrategyCatalogKind.Family, "benchmark-" + Guid.NewGuid().ToString("N"), "Isolated qualification family");
        var hash = await database.InsertStrategyCatalogDraftAsync(row, 0, "benchmark").ConfigureAwait(false);
        await publish(row.Key, hash, At, "benchmark", CancellationToken.None).ConfigureAwait(false);
        return row.Key;
    }
}
