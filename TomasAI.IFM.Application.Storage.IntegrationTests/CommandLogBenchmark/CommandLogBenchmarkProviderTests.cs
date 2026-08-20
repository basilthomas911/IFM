using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.CommandLogBenchmark;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.CommandLogBenchmark;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CommandLogBenchmarkCollection : ICollectionFixture<CommandLogBenchmarkFixture>
{
    public const string Name = "Command-log PostgreSQL/ScyllaDB comparison";
}

[Collection(CommandLogBenchmarkCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CommandLogBenchmarkProviderTests(CommandLogBenchmarkFixture fixture)
{
    [Fact]
    public async Task Both_guards_accept_one_insert_and_reject_the_duplicate()
    {
        var entry = fixture.CreateEntry();
        try
        {
            (await fixture.Postgres.TryInsertAsync(entry)).Should().BeTrue();
            (await fixture.Postgres.TryInsertAsync(entry)).Should().BeFalse();
            (await fixture.Scylla.TryInsertAsync(entry)).Should().BeTrue();
            (await fixture.Scylla.TryInsertAsync(entry)).Should().BeFalse();

            var postgres = await fixture.Postgres.GetAsync(entry.CommandId);
            postgres.Should().NotBeNull();
            postgres!.JsonCommandData.Should().Be(entry.JsonCommandData);
            postgres.CommandTimestampUtc.Should().BeCloseTo(entry.CommandTimestampUtc, TimeSpan.FromMilliseconds(1));
            postgres.CommandTimestampUtc.Kind.Should().Be(DateTimeKind.Utc);

            var scylla = await fixture.Scylla.GetAsync(entry.CommandId);
            scylla.Should().NotBeNull();
            scylla!.MessagePackCommandData.Should().Equal(entry.MessagePackCommandData);
            scylla.CommandTimestampUtc.Should().BeCloseTo(entry.CommandTimestampUtc, TimeSpan.FromMilliseconds(1));
            scylla.CommandTimestampUtc.Kind.Should().Be(DateTimeKind.Utc);
            new MessagePackBinarySerializer()
                .Deserialize<BenchmarkCommand>(scylla.MessagePackCommandData)
                .Should().Be(new BenchmarkCommand("ESU6", 42));
        }
        finally
        {
            await fixture.DeleteAsync(entry.CommandId);
        }
    }

    [Fact]
    public async Task Concurrent_duplicates_have_exactly_one_winner_per_provider()
    {
        var postgresEntry = fixture.CreateEntry();
        var scyllaEntry = fixture.CreateEntry();
        try
        {
            var postgresResults = await Task.WhenAll(
                Enumerable.Range(0, 32).Select(_ => fixture.Postgres.TryInsertAsync(postgresEntry)));
            var scyllaResults = await Task.WhenAll(
                Enumerable.Range(0, 32).Select(_ => fixture.Scylla.TryInsertAsync(scyllaEntry)));

            postgresResults.Count(applied => applied).Should().Be(1);
            scyllaResults.Count(applied => applied).Should().Be(1);
        }
        finally
        {
            await fixture.DeleteAsync(postgresEntry.CommandId);
            await fixture.DeleteAsync(scyllaEntry.CommandId);
        }
    }
}

public sealed class CommandLogBenchmarkFixture : IAsyncLifetime
{
    const string ScyllaConnectionVariable = "IFM_SCYLLA_TEST_CONNECTION";
    const string PostgresConnectionVariable = "IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION";

    public ScyllaCommandLogBenchmarkStore Scylla { get; private set; } = null!;
    public PostgresCommandLogBenchmarkStore Postgres { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var scyllaConnection = Required(ScyllaConnectionVariable);
        var postgresConnection = Required(PostgresConnectionVariable);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");

        var settings = new DbConnectionSettings()
            .Add(
                ScyllaCommandLogBenchmarkStore.ConnectionName,
                scyllaConnection,
                "System.Data.ScyllaDb")
            .Add(
                EventSourceActorDbContext.EventSourceActorDbConnection,
                postgresConnection,
                "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        Scylla = new ScyllaCommandLogBenchmarkStore(settings, logger);
        Postgres = new PostgresCommandLogBenchmarkStore(settings, logger);
        await Scylla.CreateSchemaAsync();
        await Postgres.CreateSchemaAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public CommandLogBenchmarkEntry CreateEntry()
        => CommandLogBenchmarkEntry.Create(
            Guid.NewGuid(),
            $"benchmark-{Guid.NewGuid():N}",
            "MarketData",
            nameof(BenchmarkCommand),
            DateTime.UtcNow,
            new BenchmarkCommand("ESU6", 42));

    public async Task DeleteAsync(Guid commandId)
    {
        await Postgres.DeleteAsync(commandId);
        await Scylla.DeleteAsync(commandId);
    }

    static string Required(string variable)
        => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set {variable} to a dedicated integration-test database connection.");
}

public sealed record BenchmarkCommand(string ContractId, int Sequence);
