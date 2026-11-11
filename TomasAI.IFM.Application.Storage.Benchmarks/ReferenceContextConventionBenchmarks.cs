using TomasAI.IFM.Shared.MarketData.Definitions;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures exact definition reads and idempotent ordinal batch writes on ReferenceDb.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
public class ReferenceContextConventionBenchmarks
{
    ReferenceDbContext database = null!;
    ExactInstrumentDefinition[] definitions = null!;
    Guid snapshot;
    const string Dataset = "GLBX.MDP3";

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
            await using var durabilityRow = await command.ExecuteReaderAsync();
            await durabilityRow.ReadAsync();
            if (Enumerable.Range(0, 3).Any(index => durabilityRow.GetString(index) != "on"))
                throw new InvalidOperationException("Qualification requires full WAL durability.");
        }
        builder.Pooling = !ColdConnection;
        builder.Username = "";
        builder.Password = "";
        var settings = new DbConnectionSettings().Add(ReferenceDbContext.ReferenceDbConnection,
            builder.ConnectionString, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new ReferenceSchemaDb(settings, logger).CreateAllAsync();
        var factory = new DbContextFactory(new DbContextResolver(_ => database));
        database = new(settings, factory, null!, logger);
        var row = ExactInstrumentDefinition.Parse(Dataset, """
            {"hd":{"rtype":19,"publisher_id":1,"instrument_id":42002230,"ts_event":"1788089407203111341"},"ts_recv":"1788480000000000000","raw_symbol":"OZRU7 P2020","asset":"OZR","instrument_class":"P","underlying_id":42000681,"currency":"USD","exchange":"XCBT","expiration":"1819390800000000000","activation":"1784928600000000000","strike_price":"2020000000000"}
            """);
        definitions = Enumerable.Repeat(row, 32).ToArray();
        snapshot = Guid.NewGuid();
        await WriteIdempotentBatch();
        if (!string.Equals(await ReadExactDefinition(), row.Json, StringComparison.Ordinal))
            throw new InvalidOperationException("Exact instrument definition qualification failed.");
    }

    [Benchmark] public Task<string?> ReadExactDefinition() =>
        database.GetInstrumentDefinitionJsonAsync(snapshot, Dataset, 0, 128);

    [Benchmark] public Task WriteIdempotentBatch() =>
        database.InsertInstrumentDefinitionsAsync(snapshot, 127, definitions, CancellationToken.None);
}
