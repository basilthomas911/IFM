using Microsoft.Extensions.Logging.Abstractions;
using MessagePack;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.EventSourcing.Projections.BrokerAccount;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Domain.BrokerAccount.IntegrationTests;

[CollectionDefinition(nameof(BrokerAccountPostgresCollection))]
public sealed class BrokerAccountPostgresCollection : ICollectionFixture<EventSourceActorSnapshotRangeFixture>;

[Collection(nameof(BrokerAccountPostgresCollection))]
public sealed class BrokerAccountPostgresProjectionTests(EventSourceActorSnapshotRangeFixture fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Positional_parameters_preserve_highest_revision_and_idempotent_replay()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var schema = new NpgsqlCommand("""
            CREATE SCHEMA trade;
            CREATE TABLE trade.broker_account_read_model (
                account_alias text, revision bigint, account_definition bytea,
                PRIMARY KEY (account_alias, revision)
            );
            """, connection);
        await schema.ExecuteNonQueryAsync();

        var providerConnection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = string.Empty,
            Password = string.Empty
        };
        var settings = new DbConnectionSettings()
            .Add("TradeDbConnection", providerConnection.ConnectionString, "System.Data.Postgres");
        var id = new BrokerAccountId("ISOLATED-TEST");
        var account = new BrokerAccountDefinition
        {
            Id = id,
            Revision = 2,
            Environment = BrokerEnvironment.Emulator,
            QualificationStatus = BrokerAccountQualificationStatus.Accepted,
            Gate = BrokerAccountOperationalGate.Open
        };
        var historical = account with { Revision = 1, Gate = BrokerAccountOperationalGate.Closed };
        await using var seed = new NpgsqlCommand("""
            INSERT INTO trade.broker_account_read_model (account_alias, revision, account_definition)
            VALUES ($1, 1, $2);
            """, connection);
        seed.Parameters.Add(new NpgsqlParameter { Value = id.AccountAlias });
        seed.Parameters.Add(new NpgsqlParameter { Value = MessagePackSerializer.Serialize(historical) });
        await seed.ExecuteNonQueryAsync();

        var tradeDb = new TradeDbContext(settings, Substitute.For<IDbContextFactory>(),
            Substitute.For<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
        Assert.Equal(historical, await tradeDb.DbReader.GetBrokerAccountAsync(id));
        var development = new BrokerAccountProjectionService(tradeDb.DbReader, tradeDb.DbWriter, isDevelopment: true);
        await development.ProjectAsync(account with { DevelopmentQualificationsExempt = true });
        await development.ProjectAsync(historical);
        await development.ProjectAsync(account);
        Assert.Equal(account with { DevelopmentQualificationsExempt = true }, await development.GetAsync(id));
        Assert.Equal(account with { DevelopmentQualificationsExempt = true }, development.Get(id));
        Assert.Null(await development.GetAsync(new("MISSING")));

        var reopened = new TradeDbContext(settings, Substitute.For<IDbContextFactory>(),
            Substitute.For<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
        Assert.Equal(account, await reopened.DbReader.GetBrokerAccountAsync(id));
        Assert.Null(await reopened.DbReader.GetBrokerAccountAsync(new("MISSING")));
        var production = new BrokerAccountProjectionService(reopened.DbReader, reopened.DbWriter, isDevelopment: false);
        Assert.Equal(account, await production.GetAsync(id));

        await using var history = new NpgsqlCommand("""
            SELECT revision, account_definition FROM trade.broker_account_read_model ORDER BY revision;
            """, connection);
        await using var rows = await history.ExecuteReaderAsync();
        Assert.True(await rows.ReadAsync());
        Assert.Equal(1L, rows.GetInt64(0));
        Assert.Equal(historical, MessagePackSerializer.Deserialize<BrokerAccountDefinition>(rows.GetFieldValue<byte[]>(1)));
        Assert.True(await rows.ReadAsync());
        Assert.Equal(2L, rows.GetInt64(0));
        Assert.Equal(account, MessagePackSerializer.Deserialize<BrokerAccountDefinition>(rows.GetFieldValue<byte[]>(1)));
        Assert.False(await rows.ReadAsync());
    }
}
