using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
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
        var store = new BrokerAccountReadStore(settings, NullLogger<DbProvider>.Instance);
        var id = new BrokerAccountId("ISOLATED-TEST");
        var account = new BrokerAccountDefinition
        {
            Id = id,
            Revision = 2,
            QualificationStatus = BrokerAccountQualificationStatus.Accepted,
            Gate = BrokerAccountOperationalGate.Open
        };
        await store.ProjectAsync(account);
        await store.ProjectAsync(account with { Revision = 1, Gate = BrokerAccountOperationalGate.Closed });
        await store.ProjectAsync(account);

        Assert.Equal(account, await new BrokerAccountReadStore(settings, NullLogger<DbProvider>.Instance).GetAsync(id));
        Assert.Null(await store.GetAsync(new("MISSING")));
    }
}
