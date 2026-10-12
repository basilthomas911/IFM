using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.OptionPricerDb;

[CollectionDefinition("OptionPricer PostgreSQL", DisableParallelization = true)]
public sealed class OptionPricerPostgresCollection : ICollectionFixture<OptionPricerPostgresFixture>;

/// <summary>Owns disposable infrastructure shared by the pricing CRUD and event-atomicity tests.</summary>
public sealed class OptionPricerPostgresFixture : IAsyncLifetime
{
    readonly IsolatedIntegrationInfrastructure infrastructure = new("optionpricerconventions");
    long nextId;
    public string Connection => infrastructure.PostgresConnectionString;
    public DbConnectionSettings Settings { get; private set; } = null!;
    public OptionPricerDbContext DevDatabase { get; private set; } = null!;
    public PostgresEventTransaction Transactions { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await infrastructure.StartAsync();
        Settings = (DbConnectionSettings)new DbConnectionSettings()
            .Add(OptionPricerDbContext.ConnectionName, Connection, "System.Data.Postgres")
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, Connection, "System.Data.Postgres");
        await new OptionPricerSchemaDb(Settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        await new EventSourceSchemaDb(Settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var sequence = Substitute.For<ISequenceIdGenerator>();
        sequence.GetSequenceIdAsync(SequenceName.SpreadDistribution_Id)
            .Returns(_ => new ValueTask<long>(Interlocked.Increment(ref nextId)));
        DevDatabase = new(Settings, Substitute.For<IDbContextFactory>(), sequence, NullLogger<DbProvider>.Instance);
        Transactions = new(Settings);
    }

    public async Task DisposeAsync() => await infrastructure.DisposeAsync();

    public async Task<long> EventCountAsync(string stream)
    {
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM event_log l JOIN event_stream_id s ON l.eventstreamid=s.eventstreamid WHERE s.eventstream=$1;", connection);
        command.Parameters.AddWithValue(stream);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
