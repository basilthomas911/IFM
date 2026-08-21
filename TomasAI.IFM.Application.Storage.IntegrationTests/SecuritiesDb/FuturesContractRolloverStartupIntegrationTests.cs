using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SecuritiesDb;

[Collection(SecuritiesDatabaseNonParallelCollection.Name)]
public sealed class FuturesContractRolloverStartupIntegrationTests(
    SecuritiesDatabaseFixture fixture) :
    IClassFixture<SecuritiesDatabaseFixture>,
    IAsyncLifetime
{
    private static readonly DateOnly ValueDate = new(2026, 8, 14);
    private readonly List<(FuturesContractRolloverReadModel Rollover, FuturesContractV2ReadModel Contract)>
        _originalAssignments = [];

    [Fact]
    public async Task StartupSeedsResolvesPersistsAndReusesValidCurrentRows()
    {
        await ResetAsync();
        var resolver = new FakeResolver();
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));
        var api = new DatabentoMarketDataApi(
            Substitute.For<IDatabentoMarketDataEpochFactory>(),
            new DatabentoMarketDataApiOptions(),
            clock,
            resolver,
            fixture.Db);
        var runtimeOptions = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(
                FeedDeploymentProfile.Development, "GLBX.MDP3") with
            {
                DataSource = FeedDataSourceMode.DatabentoLive
            },
            Contracts = []
        };
        var registry = new DatabentoContractRegistrationRegistry([], runtimeOptions);
        var check = new FuturesContractRolloverStartupCheck(
            api, fixture.Db, clock, runtimeOptions, registry);

        var first = await check.ExecuteAsync(ValueDate);
        var second = await check.ExecuteAsync(ValueDate);
        var syntheticOptions = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(
                FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
            Contracts = []
        };
        var syntheticCheck = new FuturesContractRolloverStartupCheck(
            api, fixture.Db, clock, syntheticOptions, registry);
        var synthetic = await syntheticCheck.ExecuteAsync(new DateOnly(2026, 9, 18));

        first.Should().Contain(row => row.Symbol == "ES"
            && row.ContractId == "ES20260918"
            && row.NextRolloverDate == new DateOnly(2026, 9, 18));
        first.Should().Contain(row => row.Symbol == "VX"
            && row.ContractId == "VX20260916"
            && row.NextRolloverDate == new DateOnly(2026, 9, 16));
        second.Should().Contain(row => row.Symbol == "ES" && row.ContractId == "ES20260918");
        synthetic.Should().Contain(row => row.Symbol == "VX" && row.ContractId == "VX20260916");
        resolver.CallCount.Should().Be(2,
            "startup reuses valid persisted assignments until their rollover date is due");

        var es = await fixture.Db.GetFuturesContractAsync("ES20260918");
        var vx = await fixture.Db.GetFuturesContractAsync("VX20260916");
        es.Should().NotBeNull();
        es!.CurrentlyTraded.Should().BeTrue();
        vx.Should().NotBeNull();
        vx!.CurrentlyTraded.Should().BeTrue();
        registry.Should().Contain(registration =>
            registration.DomainContractId == "ES20260918"
            && registration.ProviderContractName == "ESU6"
            && registration.Dataset == "GLBX.MDP3"
            && registration.AssetTypeId == AssetTypeId.Futures);
        registry.Should().Contain(registration =>
            registration.DomainContractId == "VX20260916"
            && registration.ProviderContractName == "VX/U6"
            && registration.Dataset == "XCBF.PITCH"
            && registration.AssetTypeId == AssetTypeId.Futures);
    }

    [Fact]
    public async Task UpdateReturnsTrueWhenDateIsSetAndFalseWhileNotDue()
    {
        await ResetAsync();
        var resolver = new FakeResolver();
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));
        await fixture.Db.EnsureFuturesContractRolloverRowsAsync(
            ["ES"], clock.GetUtcNow().UtcDateTime, "integration-test");
        var api = new DatabentoMarketDataApi(
            Substitute.For<IDatabentoMarketDataEpochFactory>(),
            new DatabentoMarketDataApiOptions(),
            clock,
            resolver,
            fixture.Db);

        var set = await api.UpdateCurrentlyTradedFuturesContractAsync("ES", ValueDate);
        var skipped = await api.UpdateCurrentlyTradedFuturesContractAsync("ES", ValueDate);

        set.Should().BeTrue();
        skipped.Should().BeFalse();
        resolver.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task DueDateReplacesThePriorCurrentContractAndAdvancesRollover()
    {
        await ResetAsync();
        var resolver = new FakeResolver();
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));
        await fixture.Db.EnsureFuturesContractRolloverRowsAsync(
            ["ES"], clock.GetUtcNow().UtcDateTime, "integration-test");
        var api = new DatabentoMarketDataApi(
            Substitute.For<IDatabentoMarketDataEpochFactory>(),
            new DatabentoMarketDataApiOptions(),
            clock,
            resolver,
            fixture.Db);
        await api.UpdateCurrentlyTradedFuturesContractAsync("ES", ValueDate);

        var advanced = await api.UpdateCurrentlyTradedFuturesContractAsync(
            "ES", new DateOnly(2026, 9, 18));

        advanced.Should().BeTrue();
        (await fixture.Db.GetFuturesContractAsync("ES20260918")).Should().BeNull();
        var replacement = await fixture.Db.GetFuturesContractAsync("ES20261218");
        replacement.Should().NotBeNull();
        replacement!.CurrentlyTraded.Should().BeTrue();
        var row = await fixture.Db.GetFuturesContractRolloverAsync("ES");
        row!.NextRolloverDate.Should().Be(new DateOnly(2026, 12, 18));
    }

    [Fact]
    public async Task StartupFailsWhenARequiredSymbolCannotBeResolved()
    {
        await ResetAsync();
        var resolver = new FakeResolver(failVx: true);
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));
        var api = new DatabentoMarketDataApi(
            Substitute.For<IDatabentoMarketDataEpochFactory>(),
            new DatabentoMarketDataApiOptions(),
            clock,
            resolver,
            fixture.Db);
        var runtimeOptions = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(
                FeedDeploymentProfile.Development, "GLBX.MDP3") with
            {
                DataSource = FeedDataSourceMode.DatabentoLive
            },
            Contracts = []
        };
        var check = new FuturesContractRolloverStartupCheck(
            api, fixture.Db, clock, runtimeOptions);

        var act = () => check.ExecuteAsync(ValueDate);

        await act.Should().ThrowAsync<CurrentlyTradedFuturesContractNotFoundException>();
    }

    [Fact]
    public async Task SyntheticStartupBootstrapsConfiguredContractsWithoutProviderCalls()
    {
        await ResetAsync();
        var resolver = new FakeResolver(failVx: true);
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero));
        var api = new DatabentoMarketDataApi(
            Substitute.For<IDatabentoMarketDataEpochFactory>(),
            new DatabentoMarketDataApiOptions(),
            clock,
            resolver,
            fixture.Db);
        DatabentoContractRegistration[] registrations =
        [
            new()
            {
                DomainContractId = "ES20260918",
                ProviderContractName = "ESU6",
                AssetTypeId = AssetTypeId.Futures,
                RootSymbol = "ES",
                Dataset = "GLBX.MDP3"
            },
            new()
            {
                DomainContractId = "VX20260916",
                ProviderContractName = "VX/U6",
                AssetTypeId = AssetTypeId.Futures,
                RootSymbol = "VX",
                Dataset = "XCBF.PITCH"
            }
        ];
        var runtimeOptions = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(
                FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
            Contracts = registrations
        };
        var registry = new DatabentoContractRegistrationRegistry(
            registrations, runtimeOptions);
        var check = new FuturesContractRolloverStartupCheck(
            api, fixture.Db, clock, runtimeOptions, registry);

        var rows = await check.ExecuteAsync(new DateOnly(2026, 8, 19));

        resolver.CallCount.Should().Be(0);
        rows.Should().Contain(row => row.Symbol == "ES"
            && row.ContractId == "ES20260918"
            && row.NextRolloverDate == new DateOnly(2026, 9, 18));
        rows.Should().Contain(row => row.Symbol == "VX"
            && row.ContractId == "VX20260916"
            && row.NextRolloverDate == new DateOnly(2026, 9, 16));
        registry.TryGetCurrentlyTradedFuturesContract("ES", out var es).Should().BeTrue();
        es.ContractId.Should().Be("ES20260918");
        registry.TryGetCurrentlyTradedFuturesContract("VX", out var vx).Should().BeTrue();
        vx.ContractId.Should().Be("VX20260916");
    }

    private async Task ResetAsync()
    {
        foreach (var symbol in FuturesContractRolloverStartupCheck.RequiredSymbols)
        {
            var existing = await fixture.Db.GetCurrentlyTradedFuturesContractsAsync(symbol);
            foreach (var contract in existing)
                await fixture.Db.DeleteFuturesContractAsync(contract.ContractId);
            await fixture.Db.Database
                .Use(SecuritiesDbCql.DeleteFuturesContractRollover)
                .SetParameters(new DeleteFuturesContractRollover(symbol))
                .ExecuteCommandAsync();
        }
    }

    public async Task InitializeAsync()
    {
        foreach (var symbol in FuturesContractRolloverStartupCheck.RequiredSymbols)
        {
            var row = await fixture.Db.GetFuturesContractRolloverAsync(symbol);
            if (row?.ContractId is not { Length: > 0 })
                continue;
            var contract = await fixture.Db.GetPersistedFuturesContractAsync(row.ContractId);
            if (contract is not null)
                _originalAssignments.Add((row, contract));
        }
        await ResetAsync();
    }

    public async Task DisposeAsync()
    {
        await ResetAsync();
        await fixture.Db.EnsureFuturesContractRolloverRowsAsync(
            FuturesContractRolloverStartupCheck.RequiredSymbols,
            DateTime.UtcNow,
            nameof(FuturesContractRolloverStartupIntegrationTests));
        foreach (var assignment in _originalAssignments)
        {
            await fixture.Db.ReplaceCurrentlyTradedFuturesContractAsync(
                assignment.Rollover,
                assignment.Contract);
        }
    }

    private sealed class FakeResolver(bool failVx = false) : IDatabentoCurrentFuturesContractResolver
    {
        internal int CallCount { get; private set; }

        public Task<ResolvedCurrentFuturesContract> ResolveAsync(
            string symbol,
            DateOnly valueDate,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (failVx && symbol == "VX")
                throw new CurrentlyTradedFuturesContractNotFoundException(symbol, valueDate);
            var maturity = symbol == "VX"
                ? new DateOnly(2026, 9, 16)
                : valueDate >= new DateOnly(2026, 9, 18)
                    ? new DateOnly(2026, 12, 18)
                    : new DateOnly(2026, 9, 18);
            var contract = new FuturesContractV2ReadModel(
                $"{symbol}{maturity:yyyyMMdd}",
                $"{symbol} integration contract",
                symbol,
                symbol == "VX" ? "VX/U6" : maturity.Month == 12 ? "ESZ6" : "ESU6",
                "FUT",
                "USD",
                symbol == "VX" ? "CFE" : "CME",
                symbol == "VX" ? "1000" : "50",
                maturity,
                true);
            return Task.FromResult(new ResolvedCurrentFuturesContract(contract, maturity));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
