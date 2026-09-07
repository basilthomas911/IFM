using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MarketConditionConfigurationDbCollection
    : ICollectionFixture<MarketConditionConfigurationDbFixture>
{
    public const string Name = "MC-02 ConfigurationDb PostgreSQL integration";
}

public sealed class MarketConditionConfigurationDbFixture : IAsyncLifetime
{
    const string ConnectionVariable = "IFM_POSTGRES_CONFIGURATION_TEST_CONNECTION";
    // Keep generated configuration fixtures out of the Development API database.
    const string DefaultConnection = "Host=localhost;Port=5432;Database=ifm-configuration-integration-tests";
    const string Provider = "System.Data.Postgres";

    public ConfigurationDbContext Context { get; private set; } = null!;
    public MarketConditionConfigurationTestRepository Repository { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = DefaultConnection;

        var settings = new DbConnectionSettings()
            .Add(ConfigurationDbContext.ConfigurationDbConnection, connectionString, Provider);
        var logger = Substitute.For<ILogger<DbProvider>>();
        await new ConfigurationSchemaDb(settings, logger).CreateAllAsync();

        var contexts = new Dictionary<Type, object>();
        var factory = new DbContextFactory(new DbContextResolver(type => contexts[type]));
        Context = new ConfigurationDbContext(settings, factory, logger);
        contexts.Add(typeof(IObjectRepository<ConfigurationDbContext>), Context);
        Repository = new MarketConditionConfigurationTestRepository(
            settings[ConfigurationDbContext.ConfigurationDbConnection], logger);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public sealed class MarketConditionConfigurationTestRepository(
    IDbConnectionSetting connectionSetting,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<MarketConditionConfigurationTestRepository>(connectionSetting, logger)
{
    public override IObjectRepository Database => this;
}

[Trait("Category", "Integration")]
[Trait("Gate", "MC-02")]
[Collection(MarketConditionConfigurationDbCollection.Name)]
public sealed class MarketConditionConfigurationDbTests(MarketConditionConfigurationDbFixture fixture)
{
    [Fact]
    public async Task Draft_insert_and_exact_read_round_trip_typed_payload_and_canonical_hash()
    {
        var parameterSet = ParameterSet();

        await fixture.Context.InsertMarketConditionDraftAsync(parameterSet, "MC-02 exact read", "mc-02-tests");
        var actual = await fixture.Context.GetMarketConditionAsync(parameterSet.ParameterSetId, parameterSet.Version);

        actual.Should().NotBeNull();
        actual!.ParameterSet.Should().BeEquivalentTo(parameterSet);
        actual.PayloadJson.Should().Be(MarketConditionParameterPayload.Serialize(parameterSet));
        actual.PayloadSha256.Should().Be(MarketConditionParameterPayload.ComputeSha256(parameterSet));
        actual.EffectiveFromUtc.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public async Task Publish_selects_at_effective_time_and_excludes_future_effective_version()
    {
        var parameterSet = ParameterSet();
        var effectiveFromUtc = UtcNow().AddMinutes(5);
        await fixture.Context.InsertMarketConditionDraftAsync(parameterSet, "MC-02 future", "mc-02-tests");
        await fixture.Context.PublishAsync(StrategyParameterSetKind.MarketCondition,
            parameterSet.ParameterSetId, parameterSet.Version, effectiveFromUtc);

        var before = await fixture.Context.ResolveEffectiveMarketConditionAsync(
            effectiveFromUtc.AddTicks(-1), parameterSet.FundId, "ES", parameterSet.TargetHorizon);
        var atBoundary = await fixture.Context.ResolveEffectiveMarketConditionAsync(
            effectiveFromUtc, parameterSet.FundId, "ES", parameterSet.TargetHorizon);

        before.Should().BeNull();
        atBoundary.Should().NotBeNull();
        atBoundary!.ParameterSet.ParameterSetId.Should().Be(parameterSet.ParameterSetId);
        atBoundary.EffectiveFromUtc.Should().Be(effectiveFromUtc);
    }

    [Fact]
    public async Task Retire_excludes_version_from_new_resolution_but_preserves_exact_read()
    {
        var parameterSet = ParameterSet();
        var effectiveFromUtc = UtcNow().AddMinutes(-10);
        var retiredAtUtc = UtcNow().AddMinutes(-1);
        await fixture.Context.InsertMarketConditionDraftAsync(parameterSet, "MC-02 retire", "mc-02-tests");
        await fixture.Context.PublishAsync(StrategyParameterSetKind.MarketCondition,
            parameterSet.ParameterSetId, parameterSet.Version, effectiveFromUtc);
        await fixture.Context.RetireAsync(StrategyParameterSetKind.MarketCondition,
            parameterSet.ParameterSetId, parameterSet.Version, retiredAtUtc);

        var selected = await fixture.Context.ResolveEffectiveMarketConditionAsync(
            UtcNow(), parameterSet.FundId, "ES", parameterSet.TargetHorizon);
        var exact = await fixture.Context.GetMarketConditionAsync(parameterSet.ParameterSetId, parameterSet.Version);

        selected.Should().BeNull();
        exact.Should().NotBeNull();
        exact!.ParameterSet.Should().BeEquivalentTo(parameterSet);
    }

    [Fact]
    public async Task Overlapping_published_matches_are_rejected_as_ambiguous()
    {
        var fundId = FundId();
        var older = ParameterSet(fundId: fundId);
        var newer = ParameterSet(fundId: fundId);
        await fixture.Context.InsertMarketConditionDraftAsync(older, "MC-02 ambiguity older", "mc-02-tests");
        await fixture.Context.InsertMarketConditionDraftAsync(newer, "MC-02 ambiguity newer", "mc-02-tests");
        await fixture.Context.PublishAsync(StrategyParameterSetKind.MarketCondition,
            older.ParameterSetId, older.Version, UtcNow().AddMinutes(-2));
        await fixture.Context.PublishAsync(StrategyParameterSetKind.MarketCondition,
            newer.ParameterSetId, newer.Version, UtcNow().AddMinutes(-1));

        var action = () => fixture.Context.ResolveEffectiveMarketConditionAsync(
            UtcNow(), fundId, "ES", TimeFrameType.Daily);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ambiguous*");
    }

    [Fact]
    public async Task Missing_and_invalid_lifecycle_transitions_are_rejected()
    {
        var missing = ParameterSet();
        var parameterSet = ParameterSet();
        var effectiveFromUtc = UtcNow().AddMinutes(-1);

        await FluentActions.Invoking(() => fixture.Context.PublishAsync(StrategyParameterSetKind.MarketCondition,
                missing.ParameterSetId, missing.Version, effectiveFromUtc))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*exactly one Draft*");

        await fixture.Context.InsertMarketConditionDraftAsync(parameterSet, "MC-02 lifecycle", "mc-02-tests");
        await FluentActions.Invoking(() => fixture.Context.RetireAsync(StrategyParameterSetKind.MarketCondition,
                parameterSet.ParameterSetId, parameterSet.Version, UtcNow()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*exactly one Published*");

        await fixture.Context.PublishAsync(StrategyParameterSetKind.MarketCondition,
            parameterSet.ParameterSetId, parameterSet.Version, effectiveFromUtc);
        await FluentActions.Invoking(() => fixture.Context.PublishAsync(StrategyParameterSetKind.MarketCondition,
                parameterSet.ParameterSetId, parameterSet.Version, effectiveFromUtc))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*exactly one Draft*");
        await FluentActions.Invoking(() => fixture.Context.RetireAsync(StrategyParameterSetKind.MarketCondition,
                parameterSet.ParameterSetId, parameterSet.Version, effectiveFromUtc.AddTicks(-1)))
            .Should().ThrowAsync<StorageException>();

        await fixture.Context.RetireAsync(StrategyParameterSetKind.MarketCondition,
            parameterSet.ParameterSetId, parameterSet.Version, UtcNow());
        await FluentActions.Invoking(() => fixture.Context.RetireAsync(StrategyParameterSetKind.MarketCondition,
                parameterSet.ParameterSetId, parameterSet.Version, UtcNow()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*exactly one Published*");
    }

    [Fact]
    public async Task Published_payload_is_immutable_and_version_cannot_be_deleted()
    {
        var parameterSet = ParameterSet();
        await fixture.Context.InsertMarketConditionDraftAsync(parameterSet, "MC-02 immutable", "mc-02-tests");
        await fixture.Context.PublishAsync(StrategyParameterSetKind.MarketCondition,
            parameterSet.ParameterSetId, parameterSet.Version, UtcNow().AddMinutes(-1));

        Func<Task> update = async () =>
        {
            await fixture.Repository.Use("MarketConditionConfigurationDbTests.mutate-payload", """
                UPDATE reference_configuration.market_condition_parameter_set
                SET payload_json = jsonb_set(payload_json, '{FundId}', '999999'::jsonb)
                WHERE parameter_set_id = $1 AND version = $2;
                """)
                .SetParameters(new ParameterKey(parameterSet.ParameterSetId, parameterSet.Version))
                .ExecuteCommandAsync();
        };
        Func<Task> delete = async () =>
        {
            await fixture.Repository.Use("MarketConditionConfigurationDbTests.delete-version", """
                DELETE FROM reference_configuration.market_condition_parameter_set
                WHERE parameter_set_id = $1 AND version = $2;
                """)
                .SetParameters(new ParameterKey(parameterSet.ParameterSetId, parameterSet.Version))
                .ExecuteCommandAsync();
        };

        await update.Should().ThrowAsync<StorageException>().WithMessage("*immutable*");
        await delete.Should().ThrowAsync<StorageException>().WithMessage("*cannot be deleted*");
        var exact = await fixture.Context.GetMarketConditionAsync(parameterSet.ParameterSetId, parameterSet.Version);
        exact!.ParameterSet.FundId.Should().Be(parameterSet.FundId);
    }

    [Fact]
    public async Task Corrupt_hash_typed_schema_and_payload_identity_are_rejected()
    {
        var hashMismatch = ParameterSet();
        await InsertRawAsync(hashMismatch.ParameterSetId, hashMismatch, hash: new string('0', 64));
        await FluentActions.Invoking(() => fixture.Context.GetMarketConditionAsync(
                hashMismatch.ParameterSetId, hashMismatch.Version))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*hash is invalid*");

        var schemaMismatch = ParameterSet();
        await InsertRawAsync(schemaMismatch.ParameterSetId, schemaMismatch, schemaVersion: 2);
        await FluentActions.Invoking(() => fixture.Context.GetMarketConditionAsync(
                schemaMismatch.ParameterSetId, schemaMismatch.Version))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*identity or schema metadata*");

        var payloadIdentity = ParameterSet();
        var storedIdentity = Guid.CreateVersion7();
        await InsertRawAsync(storedIdentity, payloadIdentity);
        await FluentActions.Invoking(() => fixture.Context.GetMarketConditionAsync(
                storedIdentity, payloadIdentity.Version))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*identity or schema metadata*");
    }

    [Fact]
    public void Lifecycle_table_selection_is_closed_to_supported_typed_kinds()
    {
        var action = () => ConfigurationDbSql.PublishFor(StrategyParameterSetKind.TradeSelection);

        action.Should().Throw<NotSupportedException>();
        ConfigurationDbSql.PublishFor(StrategyParameterSetKind.MarketCondition)
            .Should().Contain("reference_configuration.market_condition_parameter_set");
    }

    async Task InsertRawAsync(
        Guid storedId,
        MarketConditionParameterSet payload,
        string? hash = null,
        short schemaVersion = 1)
    {
        var json = MarketConditionParameterPayload.Serialize(payload);
        await fixture.Repository.Use("MarketConditionConfigurationDbTests.insert-corrupt",
                ConfigurationDbSql.InsertMarketConditionDraft)
            .SetParameters(new InsertConfigurationDraft(
                storedId,
                payload.Version,
                schemaVersion,
                (short)ConfigurationParameterSetStatus.Draft,
                json,
                hash ?? MarketConditionParameterPayload.ComputeSha256(json),
                "MC-02 corrupt fixture",
                UtcNow(),
                "mc-02-tests"))
            .ExecuteCommandAsync();
    }

    static MarketConditionParameterSet ParameterSet(
        int? fundId = null,
        TimeFrameType horizon = TimeFrameType.Daily) =>
        MarketConditionParameterSet.CreateDefault(
            Guid.CreateVersion7(), Guid.CreateVersion7(), fundId ?? FundId(), horizon);

    static int FundId() => Random.Shared.Next(100_000, int.MaxValue);

    static DateTime UtcNow()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }

    readonly record struct ParameterKey(Guid ParameterSetId, int Version) : IBindValue
    {
        public object Bind() => Values(Uuid(ParameterSetId), Integer(Version));
    }
}
