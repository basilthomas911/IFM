using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using static TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb.StrategyCatalogContractTests;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb;

[Trait("Category", "Integration")]
[Collection(MarketConditionConfigurationDbCollection.Name)]
public sealed class StrategyCatalogDbTests(MarketConditionConfigurationDbFixture fixture)
{
    static readonly CatalogCapability[] StrategyCapabilities = [new("evaluator", "fixture", 1), new("data", "fixture", 1)];
    static readonly CatalogCapability[] BuilderCapabilities = [new("builder", "fixture", 1), new("risk", "fixture", 1)];
    static readonly CatalogCapability[] ValidatorCapabilities = [new("validator", "fixture", 1)];
    static DateTime Now => ConfigurationDbContext.CatalogNow();

    static ConfigurationDbContext Context(bool supported = true, IStrategyCatalogReferences? references = null)
    {
        var settings = new DbConnectionSettings().Add(ConfigurationDbContext.ConfigurationDbConnection,
            Environment.GetEnvironmentVariable("IFM_POSTGRES_CONFIGURATION_TEST_CONNECTION") ?? "Host=localhost;Port=5432;Database=ifm-configuration-integration-tests", "System.Data.Postgres");
        var validators = new[] { "evaluator", "data", "builder", "risk", "validator" }
            .Select(role => (IStrategyCatalogCapabilityValidator)new FixtureValidator(new(role, "fixture", 1)));
        return new(settings, Substitute.For<IDbContextFactory>(), Substitute.For<ILogger<DbProvider>>(),
            new StrategyCatalogCapabilityRegistry(supported ? validators : []), references ?? new FixtureReferences());
    }

    [Fact]
    public async Task Normalized_catalog_round_trip_publication_and_frozen_deployment_preserve_all_exact_versions()
    {
        var ctx = Context(); var graph = await Graph(ctx);
        var snapshot = await ctx.GetPublishedStrategyDeploymentAsync(graph.Deployment.Key, Now);
        snapshot.Definitions.Should().HaveCount(7);
        snapshot.Definitions.Select(x => x.Definition.Key).Should().Contain(graph.Schema.Key).And.Contain(graph.Parameter.Key);
        snapshot.ContentHash.Should().HaveLength(64);
        var second = await ctx.GetPublishedStrategyDeploymentAsync(graph.Deployment.Key, Now);
        second.ContentHash.Should().Be(snapshot.ContentHash);
        var stored = await ctx.GetStrategyCatalogAsync(graph.Deployment.Key);
        stored!.Definition.Products.Should().BeEquivalentTo(graph.Deployment.Products);
        stored.Definition.Parameters.Should().BeEquivalentTo(graph.Deployment.Parameters);
        stored.PublishedBy.Should().Be("catalog-tests");
        (await Scalar(ctx, "SELECT count(*) FROM reference_configuration.strategy_deployment_product WHERE owner_id=$1", graph.Deployment.Key.Id)).Should().Be(1L);
        // The definition JSON is assembled from normalized relationships, not an opaque graph column.
        (await Scalar(ctx, "SELECT settings_json::text FROM reference_configuration.strategy_catalog_version WHERE id=$1", graph.Deployment.Key.Id)).Should().Be("{}");
    }

    [Fact]
    public async Task Unsupported_future_capability_can_be_authored_but_cannot_be_published()
    {
        var ctx = Context(false);
        var d = Definition(StrategyCatalogKind.Structure) with
        {
            Code = "JadeLizard-" + Guid.NewGuid().ToString("N"), Capabilities = BuilderCapabilities,
            ExpiryGroups = [new("Front")], Legs = [new("Future", "Futures", "Buy", "None", 1, "Front")]
        };
        var hash = await ctx.InsertStrategyCatalogDraftAsync(d, 0, "catalog-tests");
        await FluentActions.Invoking(() => ctx.PublishStrategyCatalogAsync(d.Key, hash, Now, "catalog-tests"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Unsupported*");
        (await ctx.GetStrategyCatalogAsync(d.Key))!.Status.Should().Be(CatalogLifecycleStatus.Draft);
    }

    [Fact]
    public async Task Child_foreign_key_failure_rolls_back_the_whole_draft_and_identity()
    {
        var ctx = Context(); var graph = await Graph(ctx);
        var invalid = Definition(StrategyCatalogKind.Variant) with
        {
            Parent = graph.Structure.Key, Capabilities = ValidatorCapabilities,
            VariantLegs = [new("MissingLeg", "Sell", 1)]
        };
        await FluentActions.Invoking(() => ctx.InsertStrategyCatalogDraftAsync(invalid, 0, "catalog-tests"))
            .Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == "23503");
        (await ctx.GetStrategyCatalogAsync(invalid.Key)).Should().BeNull();
        (await Scalar(ctx, "SELECT count(*) FROM reference_configuration.strategy_catalog_identity WHERE id=$1", invalid.Key.Id)).Should().Be(0L);
    }

    [Fact]
    public async Task Concurrent_authoring_on_same_context_has_one_winner_and_retains_previous_version()
    {
        var ctx = Context(); var first = Definition(StrategyCatalogKind.Family);
        await ctx.InsertStrategyCatalogDraftAsync(first, 0, "catalog-tests");
        var second = first with { Key = first.Key with { Version = 2 }, Name = "Revision two" };
        var results = await Task.WhenAll(TryInsert(), TryInsert());
        results.Count(x => x).Should().Be(1);
        (await ctx.GetStrategyCatalogAsync(first.Key))!.Definition.Name.Should().Be(first.Name);
        (await ctx.GetStrategyCatalogAsync(second.Key))!.Definition.Name.Should().Be(second.Name);
        async Task<bool> TryInsert()
        {
            try { await ctx.InsertStrategyCatalogDraftAsync(second, 1, "catalog-tests"); return true; }
            catch (InvalidOperationException) { return false; }
        }
    }

    [Fact]
    public async Task Publication_requires_published_dependencies_and_matching_content_hash()
    {
        var ctx = Context(); var family = Definition(StrategyCatalogKind.Family);
        await ctx.InsertStrategyCatalogDraftAsync(family, 0, "catalog-tests");
        var structure = Structure(); await Publish(ctx, structure);
        var strategy = Definition(StrategyCatalogKind.Strategy) with { Families = [family.Key], Structures = [structure.Key], Capabilities = StrategyCapabilities };
        var hash = await ctx.InsertStrategyCatalogDraftAsync(strategy, 0, "catalog-tests");
        await FluentActions.Invoking(() => ctx.PublishStrategyCatalogAsync(strategy.Key, hash, Now, "catalog-tests"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*not effective and Published*");
        var exact = await ctx.GetStrategyCatalogAsync(family.Key);
        await FluentActions.Invoking(() => ctx.PublishStrategyCatalogAsync(family.Key, new string('0', 64), Now, "catalog-tests"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*exact Draft content hash*");
        exact!.Status.Should().Be(CatalogLifecycleStatus.Draft);
    }

    [Fact]
    public async Task Retiring_dependency_blocks_new_bindings_and_preserves_historical_content()
    {
        var ctx = Context(); var graph = await Graph(ctx);
        var snapshot = await ctx.GetPublishedStrategyDeploymentAsync(graph.Deployment.Key, Now);
        var variant = await ctx.GetStrategyCatalogAsync(graph.Variant.Key);
        await ctx.RetireStrategyCatalogAsync(graph.Variant.Key, variant!.ContentHash, Now, "retirement-test");
        await FluentActions.Invoking(() => ctx.GetPublishedStrategyDeploymentAsync(graph.Deployment.Key, Now))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*not effective and Published*");
        var historical = await ctx.GetStrategyCatalogAsync(graph.Variant.Key);
        historical!.ContentHash.Should().Be(variant.ContentHash);
        historical.RetiredBy.Should().Be("retirement-test");
        snapshot.Definitions.Single(x => x.Definition.Key == graph.Variant.Key).Status.Should().Be(CatalogLifecycleStatus.Published);
    }

    [Theory]
    [InlineData("UPDATE reference_configuration.strategy_catalog_version SET name='corrupted' WHERE id=$1")]
    [InlineData("DELETE FROM reference_configuration.strategy_catalog_version WHERE id=$1")]
    [InlineData("UPDATE reference_configuration.strategy_deployment_product SET symbol='corrupted' WHERE owner_id=$1")]
    [InlineData("DELETE FROM reference_configuration.strategy_deployment_product WHERE owner_id=$1")]
    [InlineData("INSERT INTO reference_configuration.strategy_deployment_product SELECT owner_kind,owner_id,owner_version,99999,symbol,exchange,currency FROM reference_configuration.strategy_deployment_product WHERE owner_id=$1")]
    public async Task Database_guards_prevent_content_mutation_or_child_insertion_after_sealing(string sql)
    {
        var ctx = Context(); var graph = await Graph(ctx);
        await FluentActions.Invoking(() => Execute(ctx, sql, graph.Deployment.Key.Id)).Should().ThrowAsync<PostgresException>();
        (await ctx.GetStrategyCatalogAsync(graph.Deployment.Key))!.Definition.Name.Should().Be(graph.Deployment.Name);
    }

    [Fact]
    public async Task A_deployment_cannot_choose_a_structure_outside_its_strategy_assignment()
    {
        var ctx = Context(); var graph = await Graph(ctx);
        var other = Structure(); await Publish(ctx, other);
        var variant = Definition(StrategyCatalogKind.Variant) with { Parent = other.Key, Capabilities = ValidatorCapabilities };
        await Publish(ctx, variant);
        var deployment = graph.Deployment with { Key = Key(StrategyCatalogKind.Deployment), Code = "Mismatch-" + Guid.NewGuid().ToString("N"), Variants = [variant.Key] };
        var hash = await ctx.InsertStrategyCatalogDraftAsync(deployment, 0, "catalog-tests");
        await FluentActions.Invoking(() => ctx.PublishStrategyCatalogAsync(deployment.Key, hash, Now, "catalog-tests"))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*not assigned*");
    }

    [Fact]
    public async Task Parameter_validation_runs_before_insert_and_semantic_validation_before_publication()
    {
        var ctx = Context(); var schema = Definition(StrategyCatalogKind.ParameterSchema) with { Settings = JsonSerializer.SerializeToElement(Shape()), Capabilities = ValidatorCapabilities };
        await Publish(ctx, schema);
        var invalid = Definition(StrategyCatalogKind.ParameterSet) with { Parent = schema.Key, Settings = Json("{\"threshold\":1.1,\"enabled\":true}") };
        await FluentActions.Invoking(() => ctx.InsertStrategyCatalogDraftAsync(invalid, 0, "catalog-tests")).Should().ThrowAsync<ArgumentException>();
        (await ctx.GetStrategyCatalogAsync(invalid.Key)).Should().BeNull();
        var semantic = invalid with { Settings = Json("{\"threshold\":0.5,\"enabled\":false}") };
        var hash = await ctx.InsertStrategyCatalogDraftAsync(semantic, 0, "catalog-tests");
        await FluentActions.Invoking(() => ctx.PublishStrategyCatalogAsync(semantic.Key, hash, Now, "catalog-tests"))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*fixture semantic*");
    }

    [Fact]
    public async Task External_product_mismatch_and_missing_pipeline_parameters_block_publication()
    {
        var ctx = Context(); var graph = await Graph(ctx);
        var badProduct = graph.Deployment with { Key = Key(StrategyCatalogKind.Deployment), Code = "BadProduct-" + Guid.NewGuid().ToString("N"), Products = [new(101, "NQ", "CME", "USD")] };
        var hash = await ctx.InsertStrategyCatalogDraftAsync(badProduct, 0, "catalog-tests");
        await FluentActions.Invoking(() => ctx.PublishStrategyCatalogAsync(badProduct.Key, hash, Now, "catalog-tests"))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*fixture product*");
        var badParameter = graph.Deployment with
        {
            Key = Key(StrategyCatalogKind.Deployment), Code = "BadParameter-" + Guid.NewGuid().ToString("N"),
            PipelineParameters = [new("selection", CatalogPipelineParameterKind.TradeSelection, Guid.NewGuid(), 1, new string('a', 64))]
        };
        hash = await ctx.InsertStrategyCatalogDraftAsync(badParameter, 0, "catalog-tests");
        await FluentActions.Invoking(() => ctx.PublishStrategyCatalogAsync(badParameter.Key, hash, Now, "catalog-tests"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Pipeline parameter reference*");
    }

    [Fact]
    public async Task Future_publication_is_excluded_and_list_paging_never_falls_back_to_an_older_version()
    {
        var ctx = Context(); var graph = await Graph(ctx);
        var future = graph.Deployment with { Key = graph.Deployment.Key with { Version = 2 } };
        var hash = await ctx.InsertStrategyCatalogDraftAsync(future, 1, "catalog-tests");
        await ctx.PublishStrategyCatalogAsync(future.Key, hash, Now.AddDays(1), "catalog-tests");
        await FluentActions.Invoking(() => ctx.GetPublishedStrategyDeploymentAsync(future.Key, Now)).Should().ThrowAsync<InvalidOperationException>();
        string? cursor = null; var found = new List<StrategyCatalogSummary>();
        while (true)
        {
            var page = await ctx.ListStrategyCatalogAsync(StrategyCatalogKind.Deployment, 7, cursor);
            if (page.Count == 0) break;
            found.AddRange(page); cursor = page[^1].Code;
        }
        found.Select(x => x.Key.Id).Should().OnlyHaveUniqueItems();
        found.Single(x => x.Key.Id == future.Key.Id).Key.Version.Should().Be(2);
    }

    [Fact]
    public async Task Schema_creation_is_additive_and_repeatable()
    {
        var ctx = Context(); var family = Definition(StrategyCatalogKind.Family); await Publish(ctx, family);
        var settings = new DbConnectionSettings().Add(ConfigurationDbContext.ConfigurationDbConnection, ctx.ConnectionString, ctx.ProviderName);
        await new ConfigurationSchemaDb(settings, Substitute.For<ILogger<DbProvider>>()).CreateAllAsync();
        (await ctx.GetStrategyCatalogAsync(family.Key))!.Status.Should().Be(CatalogLifecycleStatus.Published);
        fixture.Context.Should().NotBeNull();
    }

    [Fact]
    public async Task Multi_expiry_legs_round_trip_even_when_the_referenced_group_is_inserted_later()
    {
        var ctx = Context();
        var d = Structure() with
        {
            ExpiryGroups = [new("Far", "Near"), new("Near")],
            Legs = [new("FrontCall", "FuturesOption", "Sell", "Call", 1, "Near"), new("BackCall", "FuturesOption", "Buy", "Call", 1, "Far")]
        };
        await Publish(ctx, d);
        var result = await ctx.GetStrategyCatalogAsync(d.Key);
        result!.Definition.ExpiryGroups.Should().BeEquivalentTo(d.ExpiryGroups);
        result.Definition.Legs.Should().BeEquivalentTo(d.Legs);
    }

    [Fact]
    public async Task Precancelled_insert_creates_no_identity_or_version()
    {
        var ctx = Context(); var d = Definition(StrategyCatalogKind.Family);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await FluentActions.Invoking(() => ctx.InsertStrategyCatalogDraftAsync(d, 0, "catalog-tests", cancellation.Token))
            .Should().ThrowAsync<OperationCanceledException>();
        (await ctx.GetStrategyCatalogAsync(d.Key)).Should().BeNull();
    }

    [Fact]
    public async Task Concurrent_publication_has_one_terminal_transition()
    {
        var ctx = Context(); var d = Definition(StrategyCatalogKind.Family);
        var hash = await ctx.InsertStrategyCatalogDraftAsync(d, 0, "catalog-tests");
        var outcomes = await Task.WhenAll(TryPublish(), TryPublish());
        outcomes.Count(x => x).Should().Be(1);
        (await ctx.GetStrategyCatalogAsync(d.Key))!.Status.Should().Be(CatalogLifecycleStatus.Published);
        async Task<bool> TryPublish()
        {
            try { await ctx.PublishStrategyCatalogAsync(d.Key, hash, Now, "catalog-tests"); return true; }
            catch (InvalidOperationException) { return false; }
        }
    }

    static StrategyCatalogDefinition Structure() => Definition(StrategyCatalogKind.Structure) with
    {
        Capabilities = BuilderCapabilities, ExpiryGroups = [new("Front")],
        Legs = [new("Future", "Futures", "Buy", "None", 1, "Front")]
    };

    static async Task Publish(ConfigurationDbContext ctx, StrategyCatalogDefinition d)
    {
        var hash = await ctx.InsertStrategyCatalogDraftAsync(d, 0, "catalog-tests");
        await ctx.PublishStrategyCatalogAsync(d.Key, hash, Now.AddMinutes(-1), "catalog-tests");
    }

    static async Task<GraphFixture> Graph(ConfigurationDbContext ctx)
    {
        var family = Definition(StrategyCatalogKind.Family); await Publish(ctx, family);
        var structure = Structure(); await Publish(ctx, structure);
        var strategy = Definition(StrategyCatalogKind.Strategy) with { Families = [family.Key], Structures = [structure.Key], Capabilities = StrategyCapabilities }; await Publish(ctx, strategy);
        var variant = Definition(StrategyCatalogKind.Variant) with { Parent = structure.Key, Capabilities = ValidatorCapabilities, VariantLegs = [new("Future", "Sell", 1)] }; await Publish(ctx, variant);
        var schema = Definition(StrategyCatalogKind.ParameterSchema) with { Settings = JsonSerializer.SerializeToElement(Shape()), Capabilities = ValidatorCapabilities }; await Publish(ctx, schema);
        var parameter = Definition(StrategyCatalogKind.ParameterSet) with { Parent = schema.Key, Settings = Json("{\"threshold\":0.5,\"enabled\":true}") }; await Publish(ctx, parameter);
        var deployment = Definition(StrategyCatalogKind.Deployment) with
        {
            Parent = strategy.Key, Capabilities = ValidatorCapabilities, Variants = [variant.Key],
            Products = [new(101, "ES", "CME", "USD")], Parameters = [new("entry", parameter.Key)], LegacyFamilies = [new(5901, 1)]
        };
        await Publish(ctx, deployment);
        return new(structure, variant, schema, parameter, deployment);
    }
    sealed record GraphFixture(StrategyCatalogDefinition Structure, StrategyCatalogDefinition Variant,
        StrategyCatalogDefinition Schema, StrategyCatalogDefinition Parameter, StrategyCatalogDefinition Deployment);

    // Explicit isolated fixtures: these are not production trading capabilities or live product validation.
    sealed record FixtureValidator(CatalogCapability Capability) : IStrategyCatalogCapabilityValidator
    {
        public void Validate(StrategyCatalogDefinition owner, IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> dependencies)
        {
            if (owner.Key.Kind == StrategyCatalogKind.ParameterSet && !owner.Settings.GetProperty("enabled").GetBoolean())
                throw new ArgumentException("Rejected by fixture semantic parameter validator.");
        }
    }
    sealed class FixtureReferences : IStrategyCatalogReferences
    {
        public Task ValidateProductAsync(CatalogProduct product, CancellationToken cancellationToken)
        {
            if (product != new CatalogProduct(101, "ES", "CME", "USD")) throw new ArgumentException("Invalid fixture product.");
            return Task.CompletedTask;
        }
        public Task ValidateLegacyFamilyAsync(CatalogLegacyFamily family, StrategyCatalogDefinition deployment, CancellationToken cancellationToken)
        {
            if (family != new CatalogLegacyFamily(5901, 1)) throw new ArgumentException("Invalid fixture legacy mapping.");
            return Task.CompletedTask;
        }
    }
    static async Task<object?> Scalar(ConfigurationDbContext ctx, string sql, Guid id)
    {
        await using var connection = ctx.CreateConnection().As<NpgsqlConnection>(ctx.ConnectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.Add(new NpgsqlParameter<Guid> { TypedValue = id });
        return await command.ExecuteScalarAsync();
    }
    static async Task Execute(ConfigurationDbContext ctx, string sql, Guid id)
    {
        await using var connection = ctx.CreateConnection().As<NpgsqlConnection>(ctx.ConnectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.Add(new NpgsqlParameter<Guid> { TypedValue = id });
        await command.ExecuteNonQueryAsync();
    }
}
