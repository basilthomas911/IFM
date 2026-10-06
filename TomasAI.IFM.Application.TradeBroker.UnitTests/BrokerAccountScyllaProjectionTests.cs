using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.TradeBroker.UnitTests;

public sealed class BrokerAccountScyllaProjectionTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Scylla_projection_survives_new_reader_and_older_revision_replay()
    {
        var settings = new DbConnectionSettings().Add("TradeDbConnection",
            "Contact Points=localhost;Port=9042;Default Keyspace=trade_test_db", "System.Data.ScyllaDb");
        var logger = NullLogger<DbProvider>.Instance;
        await new TradeSchemaDb(settings, logger).CreateAllAsync();
        var writer = new BrokerAccountReadStore(settings, logger);
        var id = new BrokerAccountId("PROJECTION-" + Guid.NewGuid().ToString("N"));
        var account = new BrokerAccountDefinition { Id = id, Revision = 2, QualificationStatus = BrokerAccountQualificationStatus.Accepted, Gate = BrokerAccountOperationalGate.Open };
        await writer.ProjectAsync(account);
        await writer.ProjectAsync(account with { Revision = 1, Gate = BrokerAccountOperationalGate.Closed });
        await writer.ProjectAsync(account);
        var reader = new BrokerAccountReadStore(settings, logger);
        Assert.Equal(account, await reader.GetAsync(id));
        Assert.Null(await reader.GetAsync(new BrokerAccountId("MISSING-" + Guid.NewGuid().ToString("N"))));
    }
}
