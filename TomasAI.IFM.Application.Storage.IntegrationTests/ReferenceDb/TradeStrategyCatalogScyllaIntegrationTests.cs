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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "TradeStrategyCatalogScylla")]
    public async Task Real_catalog_persistence_and_legacy_bootstrap_are_restart_safe(bool migrateLegacy)
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
            if (migrateLegacy)
            {
                // Reproduce the persisted v2 startup path, not in-memory DateTime fixtures.
                var audit = new DateTimeOffset(2026, 8, 31, 1, 45, 45, TimeSpan.Zero).AddMilliseconds(522);
                foreach (var (seed, index) in TradeStrategyFamilySeed.Definitions.Select((seed, index) => (seed, index)))
                    await Factory().ReferenceDb.Use("CatalogTest.InsertLegacy", "INSERT INTO trade_strategy_family_v2(catalog,tradestrategyfamilyid,definitionversion,systemkey,name,state,createdonutc,createdby) VALUES(:catalog,:id,:version,:systemkey,:name,:state,:created,:createdby);")
                        .SetParameters(new TestParameters(["V1", 5901 + index, 1L, seed.LegacySystemKey, seed.Description, "Active", audit.AddMilliseconds(index).ToOffset(TimeSpan.FromHours(-4)), "ReferenceBootstrap"]))
                        .ExecuteCommandAsync(ct);
                var legacy = await Factory().ReferenceDb.GetLegacyTradeStrategyFamiliesAsync(ct);
                legacy.Should().HaveCount(3).And.OnlyContain(x => x.CreatedOnUtc.Kind == DateTimeKind.Utc);
                foreach (var old in legacy)
                    old.CreatedOnUtc.Should().Be(audit.AddMilliseconds(old.TradeStrategyFamilyId - 5901).UtcDateTime);
                var migrated = await new TradeStrategyFamilyBootstrapper(Factory().ReferenceDb, ids).EnsureV1Async(ct);
                TradeStrategyFamilySeed.Validate(migrated);
                foreach (var old in legacy)
                {
                    var row = migrated.Single(x => x.TradeStrategyFamilyId == old.TradeStrategyFamilyId);
                    row.DefinitionVersion.Should().Be(old.DefinitionVersion);
                    row.CreatedOnUtc.Should().Be(old.CreatedOnUtc);
                    row.CreatedOnUtc.Kind.Should().Be(DateTimeKind.Utc);
                    row.CreatedBy.Should().Be(old.CreatedBy);
                }
                (await new TradeStrategyFamilyBootstrapper(Factory().ReferenceDb, ids).EnsureV1Async(ct)).Should().BeEquivalentTo(migrated);
                (await Factory().ReferenceDb.GetLegacyTradeStrategyFamiliesAsync(ct)).Should().BeEquivalentTo(legacy);
                allocated.Should().Be(0, "migration and restart must preserve existing IDs");
                var removeSeed = new RemoveTradeStrategyFamilyRequest { OperationId = Guid.NewGuid(), Target = TradeStrategyFamilyReference.From(migrated[0]) };
                var removedSeed = await new TradeStrategyFamilyCatalogStore(Factory(), ids).RemoveAsync(removeSeed, DateTime.UtcNow, "operator", ct);
                removedSeed.State.Should().Be(TradeStrategyFamilyState.Retired); removedSeed.DefinitionVersion.Should().Be(2);
                var afterRestart = await new TradeStrategyFamilyBootstrapper(Factory().ReferenceDb, ids).EnsureV1Async(ct);
                afterRestart.Where(x => x.TradeStrategyFamilyId == removedSeed.TradeStrategyFamilyId).MaxBy(x => x.DefinitionVersion)!.State.Should().Be(TradeStrategyFamilyState.Retired);
                (await Factory().ReferenceDb.GetTradeStrategyFamilyAsync(migrated[0].TradeStrategyFamilyId, 1, ct)).Should().Be(migrated[0]);
                return;
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
            var definition = request with { OperationId = Guid.NewGuid(), Description = "Updated weekly ES spread" };
            var change = new ChangeTradeStrategyFamilyRequest { OperationId = definition.OperationId, Target = TradeStrategyFamilyReference.From(created[0]), Definition = definition };
            var changes = await Task.WhenAll(workers.Select(factory => new TradeStrategyFamilyCatalogStore(factory, ids).ChangeAsync(change, Candidate(definition), ct)));
            changes.Distinct().Should().ContainSingle(); var changed = changes[0];
            changed.TradeStrategyFamilyId.Should().Be(created[0].TradeStrategyFamilyId); changed.DefinitionVersion.Should().Be(2);
            changed.Description.Should().Be("Updated weekly ES spread");
            (await Factory().ReferenceDb.GetTradeStrategyFamilyAsync(created[0].TradeStrategyFamilyId, 1, ct)).Should().Be(created[0]);
            (await restarted.ChangeAsync(change, Candidate(definition), ct)).Should().Be(changed);
            var staleDefinition = definition with { OperationId = Guid.NewGuid() };
            var stale = change with { OperationId = staleDefinition.OperationId, Definition = staleDefinition };
            await ((Func<Task>)(async () => await restarted.ChangeAsync(stale, Candidate(staleDefinition), ct))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*changed or was removed*");
            var conflictingDefinition = definition with { OperationId = Guid.NewGuid(), TimeFrame = TimeFrameType.Daily };
            var conflictingChange = change with { OperationId = conflictingDefinition.OperationId, Target = TradeStrategyFamilyReference.From(changed), Definition = conflictingDefinition };
            await ((Func<Task>)(async () => await restarted.ChangeAsync(conflictingChange, Candidate(conflictingDefinition), ct))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
            var remove = new RemoveTradeStrategyFamilyRequest { OperationId = Guid.NewGuid(), Target = TradeStrategyFamilyReference.From(changed) };
            var removals = await Task.WhenAll(workers.Select(factory => new TradeStrategyFamilyCatalogStore(factory, ids).RemoveAsync(remove, DateTime.UtcNow, "remover", ct)));
            removals.Distinct().Should().ContainSingle(); var retired = removals[0];
            retired.DefinitionVersion.Should().Be(3); retired.State.Should().Be(TradeStrategyFamilyState.Retired); retired.CreatedBy.Should().Be("remover");
            (await new TradeStrategyFamilyCatalogStore(Factory(), ids).RemoveAsync(remove, DateTime.UtcNow, "remover", ct)).Should().Be(retired);
            await ((Func<Task>)(async () => await restarted.RemoveAsync(remove with { Target = TradeStrategyFamilyReference.From(second) }, DateTime.UtcNow, "remover", ct)))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*OperationId*");
            var recreated = await restarted.CreateAsync(duplicate, Candidate(duplicate), ct);
            recreated.TradeStrategyFamilyId.Should().NotBe(retired.TradeStrategyFamilyId);
            var history = await Factory().ReferenceDb.GetTradeStrategyFamiliesAsync(ct);
            history.Should().HaveCount(5);
            history.Where(x => x.TradeStrategyFamilyId == retired.TradeStrategyFamilyId).Should().HaveCount(3);
            var races = await Task.WhenAll(workers.Take(2).Select(async (factory, index) =>
            {
                var edit = daily with { OperationId = Guid.NewGuid(), Description = "Racing change " + index };
                var racing = new ChangeTradeStrategyFamilyRequest { OperationId = edit.OperationId, Target = TradeStrategyFamilyReference.From(second), Definition = edit };
                try { return await new TradeStrategyFamilyCatalogStore(factory, ids).ChangeAsync(racing, Candidate(edit), ct); }
                catch (InvalidOperationException ex) when (ex.Message.Contains("changed or was removed")) { return null; }
            }));
            races.Count(x => x is not null).Should().Be(1, "only one edit of the same expected version may commit");
            (await Factory().ReferenceDb.GetTradeStrategyFamiliesAsync(ct)).Where(x => x.TradeStrategyFamilyId == second.TradeStrategyFamilyId).Should().HaveCount(2);
        }
        finally
        {
            // Only the uniquely named keyspace created by this test is removed.
            await admin.Use("TradeStrategyCatalogTest.DropKeyspace", $"DROP KEYSPACE IF EXISTS {keyspace};").ExecuteCommandAsync(CancellationToken.None);
        }
    }

    sealed class AdminRepository(IDbConnectionSetting settings, ILogger<DbProvider> logger) : ObjectDataRepository<AdminRepository>(settings, logger)
    { public override IObjectRepository Database => this; }
    readonly record struct TestParameters(object[] Values) : IBindValue { public object Bind() => Values; }
}
