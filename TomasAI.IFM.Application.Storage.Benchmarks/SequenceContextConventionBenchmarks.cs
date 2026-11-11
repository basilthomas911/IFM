using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Identity;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures complete public sequence operations on a disposable, fully durable PostgreSQL database.</summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class SequenceContextConventionBenchmarks
{
    SequenceIdDbContext context = null!;
    const SequenceName Sequence = SequenceName.Trade_OrderId;

    /// <summary>Includes physical connection setup when cold; otherwise uses the normal provider pool.</summary>
    [Params(false, true)]
    public bool ColdConnection { get; set; }

    /// <summary>Validates isolation/durability, creates the actual sequence schema and checks returned values.</summary>
    [GlobalSetup]
    public async Task Setup()
    {
        var connection = Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")
            ?? throw new InvalidOperationException("Use Invoke-SequenceContextQualification.ps1 to provision the isolated benchmark.");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Port != 58231 || settings.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Only the dedicated loopback benchmark endpoint is permitted.");
        await using (var guard = new NpgsqlConnection(connection))
        {
            await guard.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT current_database(),current_setting('fsync'),current_setting('synchronous_commit'),current_setting('full_page_writes')", guard);
            await using var row = await command.ExecuteReaderAsync();
            await row.ReadAsync();
            if (row.GetString(0) != settings.Database || Enumerable.Range(1, 3).Any(index => row.GetString(index) != "on"))
                throw new InvalidOperationException("Full WAL durability is required for qualification.");
        }
        settings.Pooling = !ColdConnection;
        settings.Username = string.Empty;
        settings.Password = string.Empty;
        var databases = new DbConnectionSettings().Add(SequenceIdDbContext.SequenceIdDbConnection, settings.ConnectionString, "System.Data.Postgres");
        await new SequenceIdSchemaDb(databases, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var factory = new DbContextFactory(new DbContextResolver(_ => context));
        context = new(databases, factory, NullLogger<DbProvider>.Instance);
        var ids = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => context.GetNextSequenceIdAsync(Sequence)));
        if (ids.Distinct().Count() != ids.Length || ids.Any(id => id <= 0)
            || await context.GetSequenceAllocationSizeAsync(Sequence) <= 0
            || await context.GetCurrentSequenceIdAsync(Sequence) < ids.Max())
            throw new InvalidOperationException("The public sequence allocation/readback verification failed.");
    }

    /// <summary>Returns the real configured allocation size through the context and provider.</summary>
    [Benchmark]
    public Task<long> ReadAllocationSize()
    {
        return context.GetSequenceAllocationSizeAsync(Sequence);
    }

    /// <summary>Reads the last allocated range through the same context used by application callers.</summary>
    [Benchmark]
    public Task<long> ReadCurrent()
    {
        return context.GetCurrentSequenceIdAsync(Sequence);
    }

    /// <summary>Allocates one durable block through the public context.</summary>
    [Benchmark]
    public Task<long> AllocateBlock()
    {
        return context.GetNextSequenceIdAsync(Sequence);
    }

    /// <summary>Allocates eight concurrent blocks; one reported operation is the complete eight-client group.</summary>
    [Benchmark]
    public Task<long[]> AllocateEightConcurrentBlocks()
    {
        return Task.WhenAll(Enumerable.Range(0, 8).Select(_ => context.GetNextSequenceIdAsync(Sequence)));
    }

}
