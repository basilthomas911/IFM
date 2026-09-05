using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

/// <summary>Real CQL/LWT integration in a newly-created, disposable keyspace; no application tables are recreated.</summary>
public sealed class TradeStrategyCatalogScyllaIntegrationTests
{
    [Fact]
    [Trait("Category", "TradeStrategyCatalogScylla")]
    public async Task Concurrent_product_ids_and_family_creation_survive_restart_and_reject_duplicate_or_conflicting_requests()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(100));
        var ct = timeout.Token;
        var keyspace = "ifm_tsc_" + Guid.NewGuid().ToString("N");
        // Generated identifier only; never accept a user/application keyspace as the cleanup target.
        if (!System.Text.RegularExpressions.Regex.IsMatch(keyspace, "^ifm_tsc_[0-9a-f]{32}$")) throw new InvalidOperationException("Unsafe test keyspace.");
        var logger = Substitute.For<ILogger<DbProvider>>();
        var adminSettings = new DbConnectionSettings().Add("admin", "Contact Points=localhost;Port=9042;Default Keyspace=system", "System.Data.ScyllaDb");
        var admin = new AdminRepository(adminSettings["admin"], logger);
        await admin.Use("TradeStrategyCatalogTest.CreateKeyspace", $"CREATE KEYSPACE {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};").ExecuteCommandAsync(ct);
        try
        {
            var settings = new DbConnectionSettings().Add(ReferenceDbContext.ReferenceDbConnection, $"Contact Points=localhost;Port=9042;Default Keyspace={keyspace}", "System.Data.ScyllaDb");
            await new ReferenceSchemaDb(settings, logger).CreateAsync(["trade_strategy_symbol_v1", "trade_strategy_family_catalog_v4", "trade_strategy_family_v2", "trade_strategy_family_v3"], ct);
            long allocated = 0;
            var ids = Substitute.For<ISequenceIdGenerator>();
            ids.GetSequenceIdAsync(Arg.Any<SequenceName>(), Arg.Any<CancellationToken>()).Returns(_ => new ValueTask<long>(Interlocked.Increment(ref allocated)));
            IDbContextFactory Factory()
            {
                var objects = new Dictionary<Type, object>();
                var factory = new DbContextFactory(new DbContextResolver(type => objects[type]));
                objects.Add(typeof(IObjectRepository<ReferenceDbContext>), new ReferenceDbContext(settings, factory, ids, logger));
                return factory;
            }
            var product = new TradeStrategyProduct(TradeStrategyFamilyType.FuturesOption, "ES", "USD", "XCME");
            var workers = Enumerable.Range(0, 6).Select(_ => Factory()).ToArray();
            var results = await Task.WhenAll(workers.Select(factory => new TradeStrategySymbolStore(factory, ids).GetOrCreateAsync(product, ct)));
            results.Select(x => x.Id).Distinct().Should().ContainSingle();
            var persisted = await new TradeStrategySymbolStore(Factory(), ids).GetOrCreateAsync(product, ct);
            persisted.Should().Be(results[0]);
            var otherExchange = await new TradeStrategySymbolStore(Factory(), ids).GetOrCreateAsync(product with { Exchange = "XTEST" }, ct);
            otherExchange.Id.Should().NotBe(persisted.Id);
            var request = new CreateTradeStrategyFamilyRequest { OperationId = Guid.NewGuid(), Family = product.Family, Strategy = TradeStrategyType.VerticalSpread, TimeFrame = TimeFrameType.Weekly, TradeStrategySymbolId = persisted.Id, Description = "Weekly ES spread" };
            TradeStrategyFamilyReadModel Candidate(CreateTradeStrategyFamilyRequest r) => new()
            {
                Family = r.Family, Strategy = r.Strategy, TimeFrame = r.TimeFrame, TradeStrategySymbolId = r.TradeStrategySymbolId,
                SystemKey = TradeStrategyFamilyReadModel.ComposeSystemKey(r.Family, r.Strategy), Symbol = persisted.Symbol, Currency = persisted.Currency,
                Exchange = persisted.Exchange, Description = r.Description, CreatedOnUtc = DateTime.UtcNow, CreatedBy = "integration-test"
            };
            var created = await Task.WhenAll(workers.Select(factory => new TradeStrategyFamilyCatalogStore(factory, ids).CreateAsync(request, Candidate(request), ct)));
            created.Select(x => x.TradeStrategyFamilyId).Distinct().Should().ContainSingle();
            created.Should().OnlyContain(x => x.DefinitionVersion == 1 && x.TradeStrategySymbolId == persisted.Id && x.Exchange == "XCME");
            var restarted = new TradeStrategyFamilyCatalogStore(Factory(), ids);
            (await restarted.CreateAsync(request, Candidate(request), ct)).Should().Be(created[0]);
            var conflict = request with { Description = "changed payload" };
            await ((Func<Task>)(async () => await restarted.CreateAsync(conflict, Candidate(conflict), ct))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*OperationId*");
            var duplicate = request with { OperationId = Guid.NewGuid() };
            await ((Func<Task>)(async () => await restarted.CreateAsync(duplicate, Candidate(duplicate), ct))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
            var daily = request with { OperationId = Guid.NewGuid(), TimeFrame = TimeFrameType.Daily };
            var second = await restarted.CreateAsync(daily, Candidate(daily), ct);
            second.SystemKey.Should().Be(created[0].SystemKey); second.TradeStrategyFamilyId.Should().NotBe(created[0].TradeStrategyFamilyId);
            var rows = await Factory().ReferenceDb.GetTradeStrategyFamiliesAsync(ct);
            rows.Should().HaveCount(2).And.Contain(created[0]).And.Contain(second);
        }
        finally
        {
            // Only the uniquely named keyspace created by this test is removed.
            await admin.Use("TradeStrategyCatalogTest.DropKeyspace", $"DROP KEYSPACE IF EXISTS {keyspace};").ExecuteCommandAsync(CancellationToken.None);
        }
    }

    sealed class AdminRepository(IDbConnectionSetting settings, ILogger<DbProvider> logger) : ObjectDataRepository<AdminRepository>(settings, logger)
    { public override IObjectRepository Database => this; }
}
