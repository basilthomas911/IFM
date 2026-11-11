using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures the same option generation reads and atomic publication before and after the ownership split.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
public class SecuritiesContextConventionBenchmarks
{
    delegate Task<IReadOnlyList<CachedOptionContractDefinitionReadModel>> ReadPartition(
        string symbol, string underlying, DateOnly expiry, IReadOnlyCollection<string>? roots, CancellationToken token);
    delegate IAsyncEnumerable<CachedOptionContractDefinitionReadModel> ReadRange(
        string symbol, DateOnly first, DateOnly last, int limit, IReadOnlyCollection<string>? roots, CancellationToken token);
    delegate Task Publish(string symbol, DateOnly first, DateOnly last,
        IReadOnlyCollection<CachedOptionContractDefinitionReadModel> definitions, CancellationToken token);

    readonly string symbol = "BENCH" + Guid.NewGuid().ToString("N");
    static readonly DateOnly First = new(2027, 2, 19);
    static readonly DateOnly Last = First.AddDays(3);
    CachedOptionContractDefinitionReadModel[] rows = null!;
    ReadPartition partition = null!;
    ReadRange range = null!;
    Publish publish = null!;
    readonly string[] roots = ["EW"];
    SecuritiesDbContext database = null!;

    [Params(false, true)]
    public bool ColdConnection { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var connection = Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")
            ?? throw new InvalidOperationException("Provision the disposable qualification database first.");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Port != 58231 || settings.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Only the disposable qualification database is allowed.");
        await using (var guard = new NpgsqlConnection(connection))
        {
            await guard.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT current_setting('fsync'),current_setting('synchronous_commit'),current_setting('full_page_writes')", guard);
            await using var row = await command.ExecuteReaderAsync();
            await row.ReadAsync();
            if (Enumerable.Range(0, 3).Any(index => row.GetString(index) != "on"))
                throw new InvalidOperationException("Qualification requires full WAL durability.");
        }

        settings.Pooling = !ColdConnection;
        settings.Username = "";
        settings.Password = "";
        var databases = new DbConnectionSettings().Add(SecuritiesDbContext.SecuritiesDbConnection, settings.ConnectionString, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new SecuritiesSchemaDb(databases, logger).CreateAllAsync();
        var factory = new DbContextFactory(new DbContextResolver(type => type == typeof(IObjectRepository<SecuritiesDbContext>)
            ? database : throw new InvalidOperationException(type.FullName)));
        database = new(databases, factory, logger);
        var readerType = Owner("SecuritiesOptionDefinitionReader");
        var publisherType = Owner("SecuritiesOptionDefinitionPublisher");
        if (readerType is null && publisherType is null)
        {
            partition = Bind<ReadPartition>(database, "GetCachedOptionContractDefinitionsAsync");
            range = Bind<ReadRange>(database, "ReadPublishedOptionDefinitionsAsync");
            publish = Bind<Publish>(database, "ReplaceOptionContractDefinitionsAsync");
        }
        else
        {
            var reader = Activator.CreateInstance(readerType!, database.DbReader, TimeProvider.System)!;
            var publisher = Activator.CreateInstance(publisherType!, database.DbWriter, TimeProvider.System)!;
            partition = Bind<ReadPartition>(reader, "GetOptionDefinitionsAsync");
            range = Bind<ReadRange>(reader, "ReadPublishedOptionDefinitionsAsync");
            publish = Bind<Publish>(publisher, "PublishAsync");
        }

        rows = Enumerable.Range(0, 128).Select(index =>
        {
            var expiry = First.AddDays(index / 32);
            return new CachedOptionContractDefinitionReadModel
            {
                Symbol = symbol, UnderlyingContractId = "ES20270319", ExpiryDate = expiry, ProviderRoot = "EW",
                OptionFamily = "weekly", RefreshedAtUtc = new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                Definition = new($"{symbol}{index}", "Fixture", symbol, $"EW {index}", "FOP", "USD", "CME", "50", expiry, 5000 + index, "Put")
            };
        }).ToArray();
        await PublishGeneration();
        if ((await ReadWarmPartition()).Count != 32 || await ReadWarmRange() != rows.Length)
            throw new InvalidOperationException("Definition publication qualification preflight failed.");
    }

    static Type? Owner(string name)
        => Type.GetType($"TomasAI.IFM.Domain.MarketData.Securities.Persistence.{name}, TomasAI.IFM.Domain.MarketData.Securities");

    static T Bind<T>(object instance, string name) where T : Delegate
        => (instance.GetType().GetMethod(name) ?? throw new MissingMethodException(instance.GetType().FullName, name))
            .CreateDelegate<T>(instance);

    [Benchmark]
    public Task<IReadOnlyList<CachedOptionContractDefinitionReadModel>> ReadWarmPartition()
        => partition(symbol, "ES20270319", First, roots, CancellationToken.None);

    [Benchmark]
    public async Task<int> ReadWarmRange()
    {
        var count = 0;
        await foreach (var _ in range(symbol, First, Last, 2048, roots, CancellationToken.None)) count++;
        return count;
    }

    [Benchmark]
    public Task PublishGeneration()
        => publish(symbol, First, Last, rows, CancellationToken.None);
}
