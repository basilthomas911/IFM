using TomasAI.IFM.Shared.EventModelActor.Contracts;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;
public sealed partial class TradeSelectionRuntimeTests
{
    static DateTime ComposerCatalogNow() { var at = DateTime.UtcNow; return new DateTime(at.Ticks - at.Ticks % 10, DateTimeKind.Utc); }
    [Fact, Trait("Gate", "OC-02")]
    public async Task OrderComposer_rules_publish_exact_versions_and_reject_invalid_profiles_in_Postgres()
    {
        await using var factory = Host(); _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        try
        {
            await factory.Services.GetRequiredService<ConfigurationSchemaDb>().CreateAllAsync();
            var db = factory.Services.GetRequiredService<IConfigurationDbContext>();
            var suffix = Guid.NewGuid().ToString("N"); var now = ComposerCatalogNow().AddSeconds(-1);
            var schema = StrategyCatalogExamples.New(StrategyCatalogKind.ParameterSchema, "OCRules-" + suffix, "Composer rules integration schema") with
                { Settings = CompositionRulesSchema.Settings(), Capabilities = [new("validator", "OrderCompositionRules", 1)] };
            var schemaHash = await db.InsertStrategyCatalogDraftAsync(schema, 0, "composer-integration");
            await db.PublishStrategyCatalogAsync(schema.Key, schemaHash, now, "composer-integration");
            var variant = StrategyCatalogExamples.Create().Single(x => x.Code == "LongFuture") with
                { Settings = JsonSerializer.SerializeToElement(new { TargetNetDelta = 1m, BalanceTolerance = .05m, MinimumWingWidth = 0m, MaximumWingWidth = 0m, SymmetricWings = true }) };
            foreach (var horizon in new[] { Domain.MarketData.Analytics.Shared.TimeFrameType.Daily, Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly, Domain.MarketData.Analytics.Shared.TimeFrameType.Monthly })
            {
                var rules = CompositionDefaultProfiles.Create([variant], horizon, new Black76ComposerPricer().Version);
                var parameter = StrategyCatalogExamples.New(StrategyCatalogKind.ParameterSet, "OC-" + horizon + "-" + suffix, "Composer integration rules") with
                    { Parent = schema.Key, Settings = JsonSerializer.SerializeToElement(rules) };
                var hash = await db.InsertStrategyCatalogDraftAsync(parameter, 0, "composer-integration");
                await db.PublishStrategyCatalogAsync(parameter.Key, hash, now, "composer-integration");
                var read = await db.GetStrategyCatalogAsync(parameter.Key);
                read!.Status.Should().Be(CatalogLifecycleStatus.Published); read.ContentHash.Should().Be(hash);
                CompositionRulesContract.Read(read.Definition.Settings.GetRawText()).SupportedHorizon.Should().Be(horizon);
                var invalid = parameter with { Key = parameter.Key with { Version = 2 }, Settings = JsonSerializer.SerializeToElement(rules with { PricerVersion = "unsupported" }) };
                var badHash = await db.InsertStrategyCatalogDraftAsync(invalid, 1, "composer-integration");
                Func<Task> publish = () => db.PublishStrategyCatalogAsync(invalid.Key, badHash, now, "composer-integration");
                await publish.Should().ThrowAsync<Exception>();
                (await db.GetStrategyCatalogAsync(invalid.Key))!.Status.Should().Be(CatalogLifecycleStatus.Draft);
                (await db.GetStrategyCatalogAsync(parameter.Key))!.ContentHash.Should().Be(hash);
                await db.RetireStrategyCatalogAsync(parameter.Key, hash, ComposerCatalogNow(), "composer-integration");
            }
            await db.RetireStrategyCatalogAsync(schema.Key, schemaHash, ComposerCatalogNow(), "composer-integration");
        }
        finally { await supervisor.ShutdownAsync(); }
    }
}
