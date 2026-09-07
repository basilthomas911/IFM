using System.Text.Json;
using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.UnitTests.Queries;

public sealed class ConfigurationStrategyCatalogTests
{
    [Fact]
    public void Defaults_have_three_named_families_with_matching_strategies_and_complete_dependencies()
    {
        var defaults = StrategyCatalogDefaults.Create();
        defaults.Where(x => x.Key.Kind == StrategyCatalogKind.Family).Select(x => x.Name)
            .Should().Equal("Futures", "Vertical Spreads", "Iron Condor");
        defaults.Where(x => x.Key.Kind == StrategyCatalogKind.Strategy).Select(x => x.Name)
            .Should().Equal("Futures", "Vertical Spreads", "Iron Condor");
        defaults.Should().NotContain(x => x.Code == "Directional" || x.Code == "RegimeAligned");
        foreach (var definition in defaults)
        {
            StrategyCatalogValidation.Freeze(definition);
            foreach (var key in definition.Families.Concat(definition.Structures).Concat(definition.Parent is null ? [] : new[] { definition.Parent }))
                defaults.Should().Contain(x => x.Key == key);
        }
        defaults.Single(x => x.Code == "DefaultVerticalSpreads").Structures.Should().HaveCount(2);
    }

    [Fact]
    public void Starter_definitions_are_valid_and_cover_both_sides_and_all_condor_biases()
    {
        var examples = StrategyCatalogExamples.Create();
        examples.Should().HaveCount(18);
        foreach (var definition in examples) StrategyCatalogValidation.Freeze(definition).Key.Should().Be(definition.Key);
        var condors = examples.Where(x => x.Key.Kind == StrategyCatalogKind.Variant && x.Parent!.Id == StrategyCatalogExamples.StableId("IronCondor")).ToArray();
        condors.Should().HaveCount(6);
        condors.Select(x => (x.Side, x.Bias)).Distinct().Should().HaveCount(6);
        condors.Where(x => x.Side == "Short").Should().OnlyContain(x => x.PremiumMode == "Credit");
        condors.Where(x => x.Side == "Long").Should().OnlyContain(x => x.PremiumMode == "Debit");
    }

    [Fact]
    public void Command_messagepack_round_trip_preserves_exact_decimal_settings_and_operation_id()
    {
        var definition = StrategyCatalogExamples.Create().First(x => x.Key.Kind == StrategyCatalogKind.Variant) with { Settings = JsonSerializer.SerializeToElement(new { Threshold = .1234567890123456789012345678m }) };
        var request = new CatalogCommandRequest(Guid.NewGuid(), CatalogCommandOperation.SaveDraft, definition);
        var command = new StrategyCatalogCommand { CommandId = request.OperationId, RequestJson = StrategyCatalogJson.Write(request), Subject = new(ActorType.Command, StrategyCatalogCommand.Actor, StrategyCatalogCommand.Verb, "default") };
        var copy = MessagePackSerializer.Deserialize<StrategyCatalogCommand>(MessagePackSerializer.Serialize(command));
        copy.CommandId.Should().Be(request.OperationId);
        var read = StrategyCatalogJson.Read<CatalogCommandRequest>(copy.RequestJson);
        read.Definition!.Settings.GetProperty("Threshold").GetDecimal().Should().Be(.1234567890123456789012345678m);
        read.Definition.Key.Should().Be(definition.Key);
    }

    [Fact]
    public void Legacy_reference_bytes_remain_readable_and_new_identity_cannot_mix_legacy_ids()
    {
        var old = MessagePackSerializer.Deserialize<TradeStrategyFamilyReference>(new byte[] { 0x92, 71, 1 });
        old.IsValid.Should().BeTrue(); old.CatalogDeployment.Should().BeNull();
        JsonSerializer.Serialize(old).Should().NotContain("CatalogDeployment");
        var key = new CatalogKey(StrategyCatalogKind.Deployment, Guid.NewGuid(), 2);
        var current = new TradeStrategyFamilyReference(0, 0) { CatalogDeployment = key };
        MessagePackSerializer.Deserialize<TradeStrategyFamilyReference>(MessagePackSerializer.Serialize(current)).Should().Be(current);
        (old with { CatalogDeployment = key }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Duplicate_save_is_idempotent_but_changed_content_cannot_replace_a_version()
    {
        var factory = Substitute.For<IDbContextFactory>(); var db = Substitute.For<IConfigurationDbContext>(); factory.ConfigurationDb.Returns(db);
        var definition = StrategyCatalogExamples.Create()[0];
        db.GetStrategyCatalogAsync(definition.Key, Arg.Any<CancellationToken>()).Returns(new StoredStrategyCatalogDefinition(definition, StrategyCatalogValidation.ContentHash(definition), CatalogLifecycleStatus.Draft, DateTime.UtcNow, "operator", null, null, null, null));
        var service = new StrategyCatalogService(factory);
        await service.ExecuteAsync(new(Guid.NewGuid(), CatalogCommandOperation.SaveDraft, definition), "operator");
        await db.DidNotReceive().InsertStrategyCatalogDraftAsync(Arg.Any<StrategyCatalogDefinition>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await FluentActions.Invoking(() => service.ExecuteAsync(new(Guid.NewGuid(), CatalogCommandOperation.SaveDraft, definition with { Name = "Different" }), "operator")).Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(CatalogLifecycleStatus.Draft)]
    [InlineData(CatalogLifecycleStatus.Retired)]
    public async Task An_unpublished_or_retired_deployment_cannot_authorize_workflow_use(CatalogLifecycleStatus status)
    {
        var key = new CatalogKey(StrategyCatalogKind.Deployment, Guid.NewGuid(), 1);
        var definition = new StrategyCatalogDefinition { Key = key, Code = "Test", Name = "Test" };
        var queries = Substitute.For<IReferenceQueryApi>();
        queries.QueryStrategyCatalogAsync(Arg.Any<CatalogQueryRequest>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<string>(StrategyCatalogJson.Write(new StoredStrategyCatalogDefinition(definition, "hash", status, DateTime.UtcNow, "test", null, null, null, null))));
        await FluentActions.Invoking(() => StrategyCatalogPermissionValidation.ValidateDeploymentAsync(queries, key, true)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Startup_keeps_defaults_and_only_explicit_migration_imports_legacy(bool importLegacy)
    {
        var factory = Substitute.For<IDbContextFactory>(); var db = Substitute.For<IConfigurationDbContext>(); factory.ConfigurationDb.Returns(db);
        var rows = new Dictionary<CatalogKey, StoredStrategyCatalogDefinition>();
        db.GetStrategyCatalogAsync(Arg.Any<CatalogKey>(), Arg.Any<CancellationToken>()).Returns(c => rows.GetValueOrDefault(c.Arg<CatalogKey>()));
        db.InsertStrategyCatalogDraftAsync(Arg.Any<StrategyCatalogDefinition>(), 0, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(c =>
        {
            var definition = StrategyCatalogValidation.Freeze(c.Arg<StrategyCatalogDefinition>()); var hash = StrategyCatalogValidation.ContentHash(definition);
            rows.Add(definition.Key, new(definition, hash, CatalogLifecycleStatus.Draft, DateTime.UtcNow, c.Arg<string>(), null, null, null, null)); return hash;
        });
        var legacy = TradeStrategyFamilySeed.Definitions[0].Create(5901, DateTime.UtcNow, "legacy");
        factory.ReferenceDb.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(new[] { legacy });
        var marketData = Substitute.For<IMarketDataApi>();
        marketData.GetTradeStrategySymbolsAsync(legacy.Family, Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([new() { Id = 101, Symbol = legacy.Symbol, Currency = legacy.Currency, Exchange = "XCME", Description = "ES" }]));
        var migration = new StrategyCatalogMigration(factory, marketData);
        if (importLegacy) { await migration.EnsureAsync(importLegacy: true); await migration.EnsureAsync(importLegacy: true); }
        else { await migration.EnsureAsync(); await migration.EnsureAsync(); }
        rows.Should().HaveCount(importLegacy ? 23 : 22); rows.Values.Should().OnlyContain(x => x.Status == CatalogLifecycleStatus.Draft);
        rows.Values.Where(x => x.Definition.Key.Kind == StrategyCatalogKind.Family).Select(x => x.Definition.Name)
            .Should().BeEquivalentTo(new[] { "Futures", "Vertical Spreads", "Iron Condor" });
        await db.DidNotReceive().PublishStrategyCatalogAsync(Arg.Any<CatalogKey>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        if (!importLegacy)
        {
            await factory.ReferenceDb.DidNotReceive().GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>());
            await marketData.DidNotReceive().GetTradeStrategySymbolsAsync(legacy.Family, Arg.Any<CancellationToken>());
            return;
        }
        var imported = rows.Values.Single(x => x.Definition.Key.Kind == StrategyCatalogKind.Deployment).Definition;
        imported.Products.Should().ContainSingle().Which.Should().Be(new CatalogProduct(101, legacy.Symbol, "XCME", legacy.Currency));
        imported.LegacyFamilies.Should().Equal(new CatalogLegacyFamily(5901, 1));
        await db.DidNotReceive().PublishStrategyCatalogAsync(Arg.Any<CatalogKey>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
