using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Identity;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.BDDTests;

public sealed class SequencePersistenceScenarios(SequenceBddDatabase database) : IClassFixture<SequenceBddDatabase>
{
    [Fact]
    public async Task Given_concurrent_allocators_When_blocks_are_reserved_Then_ids_are_unique_and_reads_do_not_advance_them()
    {
        var writer = database.Context.DbWriter;
        var reader = database.Context.DbReader;
        var name = SequenceName.Trade_OrderId;
        var increment = await reader.GetSequenceAllocationSizeAsync(name);
        var ids = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => writer.GetNextSequenceIdAsync(name)));
        var sorted = ids.Order().ToArray();
        Assert.Equal(16, ids.Distinct().Count());
        Assert.All(sorted.Zip(sorted.Skip(1)), pair => Assert.Equal(increment, pair.Second - pair.First));
        var current = await reader.GetCurrentSequenceIdAsync(name);
        Assert.Equal(sorted[^1] + increment - 1, current);
        Assert.Equal(current, await reader.GetCurrentSequenceIdAsync(name));
        await database.Schema.ValidateTablesAsync();
        Assert.Equal(current, await reader.GetCurrentSequenceIdAsync(name));
    }

    [Fact]
    public async Task Given_a_new_repository_When_the_shared_generator_restarts_Then_previously_reserved_ranges_are_not_reused()
    {
        var name = SequenceName.Trade_TradeId;
        using var first = new PostgresSequenceIdGenerator(database.Context);
        var allocated = new[] { await first.GetSequenceIdAsync(name), await first.GetSequenceIdAsync(name) };
        var through = await database.Context.DbReader.GetCurrentSequenceIdAsync(name);
        var restarted = database.CreateContext();
        using var second = new PostgresSequenceIdGenerator(restarted);
        var next = await second.GetSequenceIdAsync(name);
        Assert.True(next > through);
        Assert.DoesNotContain(next, allocated);
    }
}

public sealed class SequenceBddDatabase : IAsyncLifetime
{
    readonly IsolatedIntegrationInfrastructure infrastructure = new("sequence-convention-bdd", includeCql: false);
    IDbConnectionSettings settings = null!;
    public SequenceIdDbContext Context { get; private set; } = null!;
    public SequenceIdSchemaDb Schema { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await infrastructure.StartAsync();
        var connection = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString) { Username = string.Empty, Password = string.Empty };
        if (!connection.Database!.StartsWith("ifm_sequenceconventionbdd_", StringComparison.Ordinal))
            throw new InvalidOperationException("Sequence BDD requires its disposable database.");
        settings = new DbConnectionSettings().Add(SequenceIdDbContext.SequenceIdDbConnection, connection.ConnectionString, "System.Data.Postgres");
        Schema = new(settings, NullLogger<DbProvider>.Instance);
        await Schema.CreateAllAsync();
        Context = CreateContext();
    }

    public SequenceIdDbContext CreateContext()
    {
        SequenceIdDbContext context = null!;
        var factory = new DbContextFactory(new DbContextResolver(_ => context));
        context = new(settings, factory, NullLogger<DbProvider>.Instance);
        return context;
    }

    public async Task DisposeAsync() => await infrastructure.DisposeAsync();
}
