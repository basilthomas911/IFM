using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;
public sealed class StrategyOptionChainParameterStorageTests
{
    [Fact]
    public async Task Immutable_versions_and_monotonic_publication_roundtrip_in_isolated_Scylla_keyspace()
    {
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection,
            "Contact Points=localhost;Port=9042;Default Keyspace=option_chain_cache_verification", "System.Data.ScyllaDb");
        await new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAsync([
            "strategy_option_chain_parameter_version", "strategy_option_chain_parameter_current"]);
        var db = MarketDataDbContextTestFactory.Create(settings[MarketDataDbContext.MarketDataDbConnection]);
        var p = StrategyOptionChainParameterDefaults.IronCondor(Guid.NewGuid(), Guid.NewGuid(), 1) with { Enabled = true };
        await db.ProjectAsync(p, 2, true, default); await db.ProjectAsync(p, 2, true, default);
        Assert.Equal(p.Hash(), (await db.ReadVersionAsync(p.ParameterSetId, 1, default))!.Hash());
        Assert.Contains(await db.ReadPublishedAsync("Development", default), x => x.ParameterSetId == p.ParameterSetId);
        var second = p with { Version = 2 }; await db.ProjectAsync(second, 3, true, default);
        await db.ProjectAsync(p, 4, false, default); // Retiring an old version must not retire its replacement.
        Assert.Equal(2, (await db.ReadPublishedAsync("Development", default)).Single(x => x.ParameterSetId == p.ParameterSetId).Version);
        await db.ProjectAsync(second, 5, false, default);
        await db.ProjectAsync(second, 3, true, default); // Late replay cannot reopen a retired version.
        Assert.DoesNotContain(await db.ReadPublishedAsync("Development", default), x => x.ParameterSetId == p.ParameterSetId);
        await Assert.ThrowsAsync<InvalidDataException>(() => db.ProjectAsync(second with { Name = "conflicting" }, 6, true, default));
    }
}
