using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

internal sealed record IsolatedWorkflowCatalogFixture(CatalogKey Deployment, string Code, Guid SelectionId, Guid ConstructionId)
{
    internal static async Task<IsolatedWorkflowCatalogFixture> CreateAsync(IActorProducer producer, CancellationToken token)
    {
        Environment.GetEnvironmentVariable("IFM_NATS_URL").Should().Be("nats://127.0.0.1:24223");
        var connection = Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION") ?? "";
        var match = Regex.Match(connection, @"\AHost=127\.0\.0\.1;Port=25432;Database=ifm_eventlog_bench_([a-f0-9]{12})_synthetic_host\z");
        match.Success.Should().BeTrue("fixture publication is restricted to the disposable host");
        var settings = new DbConnectionSettings()
            .Add(ConfigurationDbContext.ConfigurationDbConnection, connection, "System.Data.Postgres")
            .Add(SequenceIdDbContext.SequenceIdDbConnection, connection, "System.Data.Postgres")
            .Add(ReferenceDbContext.ReferenceDbConnection, $"Contact Points=127.0.0.1;Port=29042;Default Keyspace=ifm_synthetic_{match.Groups[1].Value}_reference", "System.Data.ScyllaDb");
        var logger = Substitute.For<ILogger<DbProvider>>();
        var repositories = new Dictionary<Type, object>();
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        var sequenceDb = new SequenceIdDbContext(settings, factory, logger);
        repositories.Add(typeof(IObjectRepository<SequenceIdDbContext>), sequenceDb);
        var sequence = new PostgresSequenceIdGenerator(sequenceDb);
        var reference = new ReferenceDbContext(settings, factory, sequence, logger);
        repositories.Add(typeof(IObjectRepository<ReferenceDbContext>), reference);
        var configuration = new ConfigurationDbContext(settings, factory, logger);
        repositories.Add(typeof(IObjectRepository<ConfigurationDbContext>), configuration);
        var instruments = new InstrumentDefinitionStore(reference, new TradeStrategySymbolStore(factory, sequence));
        var snapshot = await instruments.GetSnapshotAsync(token);
        if (snapshot is null)
        {
            snapshot = new(Guid.NewGuid(), DateTime.UtcNow, 1, ["GLBX.MDP3"]);
            var definition = ExactInstrumentDefinition.Parse("GLBX.MDP3", """
                {"hd":{"rtype":19,"publisher_id":1,"instrument_id":900000001,"ts_event":"0"},"raw_symbol":"ESZ6","asset":"ES","instrument_class":"F","currency":"USD","exchange":"XCME","underlying_id":"0","ts_recv":"0","maturity_year":"2026","maturity_month":"12","maturity_day":"18"}
                """);
            await instruments.InsertAsync(snapshot.Id, 0, definition, token);
            await instruments.PublishAsync(snapshot, [new TradeStrategyProduct(TradeStrategyFamilyType.Futures, "ES", "USD", "XCME")], token);
        }
        var product = (await instruments.GetSymbolsAsync(snapshot.Id, TradeStrategyFamilyType.Futures, token))
            .Single(x => x.Symbol == "ES" && x.Exchange == "XCME" && x.Currency == "USD");
        var selection = TradeSelectionDefaultProfiles.Create(Guid.NewGuid(), TimeFrameType.Daily);
        var construction = new SelectionConstructionPolicy
        {
            SchemaVersion = 1, ParameterSetId = Guid.NewGuid(), Version = 1, MaximumLegs = 4,
            MinimumDaysToExpiry = 7, MaximumDaysToExpiry = 90, MinimumWingWidth = 5, MaximumWingWidth = 10,
            DeltaUnits = "UnderlyingEquivalent", MaximumDeltaTolerance = .10m
        };
        await configuration.InsertTradeSelectionDraftAsync(selection, "Synthetic qualification only", "qualification", token);
        await configuration.InsertSelectionConstructionDraftAsync(construction, "Synthetic qualification only", "qualification", token);
        var at = new DateTime(DateTime.UtcNow.AddSeconds(-1).Ticks / 10 * 10, DateTimeKind.Utc);
        await configuration.PublishAsync(StrategyParameterSetKind.TradeSelection, selection.ParameterSetId, 1, at, token);
        await configuration.PublishAsync(StrategyParameterSetKind.OrderComposition, construction.ParameterSetId, 1, at, token);

        var suffix = Guid.NewGuid().ToString("N");
        StrategyCatalogDefinition Own(StrategyCatalogDefinition d) => d with { Key = new(d.Key.Kind, Guid.NewGuid(), 1), Code = "Q" + suffix + d.Code };
        var family = Own(StrategyCatalogExamples.New(StrategyCatalogKind.Family, "Family", "Qualification Futures"));
        var structure = Own(StrategyCatalogExamples.Create().Single(x => x.Code == "Future"));
        var strategy = Own(StrategyCatalogDefaults.Create().Single(x => x.Code == "DefaultFutures")) with { Families = [family.Key], Structures = [structure.Key] };
        var variant = Own(StrategyCatalogExamples.Create().Single(x => x.Code == "LongFuture")) with
        {
            Parent = structure.Key,
            Settings = JsonSerializer.SerializeToElement(new { TargetNetDelta = 1m, BalanceTolerance = .05m, SymmetricWings = true, MinimumWingWidth = 0m, MaximumWingWidth = 0m, DeltaUnits = "UnderlyingEquivalent" })
        };
        var deployment = Own(StrategyCatalogExamples.New(StrategyCatalogKind.Deployment, "ESDaily", "Synthetic ES daily deployment")) with
        {
            Parent = strategy.Key, Horizon = TimeFrameType.Daily, Variants = [variant.Key],
            Products = [new(product.Id, product.Symbol, product.Exchange, product.Currency)],
            Capabilities = [new("validator", "StructureVariant", 1)],
            PipelineParameters = [new("selection", CatalogPipelineParameterKind.TradeSelection, selection.ParameterSetId, 1, TradeSelectionPolicy.Hash(selection)),
                new("composition", CatalogPipelineParameterKind.OrderComposition, construction.ParameterSetId, 1, construction.Hash())]
        };
        var commands = new ReferenceCommandApi(producer);
        var queries = new ReferenceQueryApi(producer);
        foreach (var definition in new[] { family, structure, strategy, variant, deployment })
        {
            var saved = await commands.ExecuteStrategyCatalogAsync(new(Guid.NewGuid(), CatalogCommandOperation.SaveDraft, definition), token);
            saved.Success.Should().BeTrue(saved.ErrorMessage);
            var published = await commands.ExecuteStrategyCatalogAsync(new(Guid.NewGuid(), CatalogCommandOperation.Publish,
                Key: definition.Key, ExpectedHash: StrategyCatalogValidation.ContentHash(definition), EffectiveUtc: at), token);
            published.Success.Should().BeTrue(published.ErrorMessage);
        }
        var validated = await queries.QueryStrategyCatalogAsync(new(CatalogQueryOperation.ValidatePublishedDeployment, Key: deployment.Key), token);
        validated.Success.Should().BeTrue(validated.ErrorMessage);
        return new(deployment.Key, deployment.Code, selection.ParameterSetId, construction.ParameterSetId);
    }
}
