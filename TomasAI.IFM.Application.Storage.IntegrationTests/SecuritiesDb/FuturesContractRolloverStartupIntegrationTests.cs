using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly List<(FuturesContractRolloverReadModel Rollover, IReadOnlyCollection<FuturesContractV3ReadModel> Contracts)>
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
        var vxBack = await fixture.Db.GetFuturesContractAsync("VX20261021");
        es.Should().NotBeNull();
        es!.OnTheRun.Should().BeTrue();
        vx.Should().NotBeNull();
        vx!.OnTheRun.Should().BeTrue();
        vxBack.Should().NotBeNull();
        vxBack!.OnTheRun.Should().BeFalse();
        vxBack.Rollover.Should().BeTrue();
        (await fixture.Db.GetRolloverFuturesContractsAsync("ES"))
            .Should().ContainSingle();
        (await fixture.Db.GetRolloverFuturesContractsAsync("VX"))
            .Should().HaveCount(2);
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

        var set = await api.UpdateOnTheRunFuturesContractAsync("ES", ValueDate);
        var skipped = await api.UpdateOnTheRunFuturesContractAsync("ES", ValueDate);

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
        await api.UpdateOnTheRunFuturesContractAsync("ES", ValueDate);

        var advanced = await api.UpdateOnTheRunFuturesContractAsync(
            "ES", new DateOnly(2026, 9, 18));

        advanced.Should().BeTrue();
        var retired = await fixture.Db.GetFuturesContractAsync("ES20260918");
        retired.Should().NotBeNull();
        retired!.OnTheRun.Should().BeFalse();
        retired.Rollover.Should().BeFalse();
        var replacement = await fixture.Db.GetFuturesContractAsync("ES20261218");
        replacement.Should().NotBeNull();
        replacement!.OnTheRun.Should().BeTrue();
        var row = await fixture.Db.GetFuturesContractRolloverAsync("ES");
        row!.NextRolloverDate.Should().Be(new DateOnly(2026, 12, 18));
    }

    [Fact]
    public async Task DueDateReplacesBothVxContractsAndAdvancesTheFrontRollover()
    {
        await ResetAsync();
        var resolver = new FakeResolver();
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));
        await fixture.Db.EnsureFuturesContractRolloverRowsAsync(
            ["VX"], clock.GetUtcNow().UtcDateTime, "integration-test");
        var api = new DatabentoMarketDataApi(
            Substitute.For<IDatabentoMarketDataEpochFactory>(),
            new DatabentoMarketDataApiOptions(),
            clock,
            resolver,
            fixture.Db);
        await api.UpdateFuturesTermStructureContractsAsync("VX", ValueDate);

        var advanced = await api.UpdateFuturesTermStructureContractsAsync(
            "VX", new DateOnly(2026, 9, 16));

        advanced.Should().BeTrue();
        var retired = await fixture.Db.GetFuturesContractAsync("VX20260916");
        retired.Should().NotBeNull();
        retired!.OnTheRun.Should().BeFalse();
        retired.Rollover.Should().BeFalse();
        var current = (await fixture.Db.GetRolloverFuturesContractsAsync("VX"))
            .OrderBy(contract => contract.LastTradeDate)
            .ToArray();
        current.Select(contract => contract.ContractId)
            .Should().Equal("VX20261021", "VX20261118");
        var row = await fixture.Db.GetFuturesContractRolloverAsync("VX");
        row!.ContractId.Should().Be("VX20261021");
        row.NextRolloverDate.Should().Be(new DateOnly(2026, 10, 21));
    }

    [Fact]
    public async Task TwoConsecutiveRolloverCyclesKeepDurablePointersAndSetsCoherent()
    {
        await ResetAsync();
        var resolver = new FakeResolver();
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));
        await fixture.Db.EnsureFuturesContractRolloverRowsAsync(
            ["ES", "VX"], clock.GetUtcNow().UtcDateTime, "two-cycle-verification");
        var api = new DatabentoMarketDataApi(
            Substitute.For<IDatabentoMarketDataEpochFactory>(),
            new DatabentoMarketDataApiOptions(),
            clock,
            resolver,
            fixture.Db);

        await api.UpdateOnTheRunFuturesContractAsync("ES", ValueDate);
        await api.UpdateFuturesTermStructureContractsAsync("VX", ValueDate);
        await api.UpdateOnTheRunFuturesContractAsync("ES", new DateOnly(2026, 9, 18));
        await api.UpdateFuturesTermStructureContractsAsync("VX", new DateOnly(2026, 9, 16));
        await api.UpdateOnTheRunFuturesContractAsync("ES", new DateOnly(2026, 12, 18));
        await api.UpdateFuturesTermStructureContractsAsync("VX", new DateOnly(2026, 10, 21));

        var esSet = await fixture.Db.GetRolloverFuturesContractsAsync("ES");
        esSet.Should().ContainSingle()
            .Which.Should().Match<FuturesContractV3ReadModel>(contract =>
                contract.ContractId == "ES20270319"
                && contract.OnTheRun
                && contract.Rollover);
        var esPointer = await fixture.Db.GetFuturesContractRolloverAsync("ES");
        esPointer!.ContractId.Should().Be("ES20270319");
        esPointer.NextRolloverDate.Should().Be(new DateOnly(2027, 3, 19));

        var vxSet = (await fixture.Db.GetRolloverFuturesContractsAsync("VX"))
            .OrderBy(contract => contract.LastTradeDate)
            .ToArray();
        vxSet.Select(contract => (contract.ContractId, contract.OnTheRun, contract.Rollover))
            .Should().Equal(
                ("VX20261118", true, true),
                ("VX20261216", false, true));
        var vxPointer = await fixture.Db.GetFuturesContractRolloverAsync("VX");
        vxPointer!.ContractId.Should().Be("VX20261118");
        vxPointer.NextRolloverDate.Should().Be(new DateOnly(2026, 11, 18));

        (await fixture.Db.GetFuturesContractAsync("ES20260918"))!
            .Rollover.Should().BeFalse();
        (await fixture.Db.GetFuturesContractAsync("ES20261218"))!
            .Rollover.Should().BeFalse();
        (await fixture.Db.GetFuturesContractAsync("VX20260916"))!
            .Rollover.Should().BeFalse();
        (await fixture.Db.GetFuturesContractAsync("VX20261021"))!
            .Rollover.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentDuplicateReplacementConvergesToOneExactVxSet()
    {
        await ResetAsync();
        var now = DateTime.UtcNow;
        await fixture.Db.EnsureFuturesContractRolloverRowsAsync(
            ["VX"], now, "concurrency-integration-test");
        var front = new FuturesContractV3ReadModel(
            "VX20260916", "VX front", "VX", "VX/U6", "FUT", "USD",
            "CFE", "1000", new DateOnly(2026, 9, 16), true, true);
        var back = new FuturesContractV3ReadModel(
            "VX20261021", "VX back", "VX", "VX/V6", "FUT", "USD",
            "CFE", "1000", new DateOnly(2026, 10, 21), false, true);
        var pointer = new FuturesContractRolloverReadModel
        {
            Symbol = "VX",
            ContractId = front.ContractId,
            NextRolloverDate = front.LastTradeDate,
            UpdatedOn = now,
            UpdatedBy = "concurrency-integration-test",
            CreatedOn = now,
            CreatedBy = "concurrency-integration-test"
        };

        await Task.WhenAll(
            fixture.Db.ReplaceFuturesRolloverSetAsync(pointer, [front, back]),
            fixture.Db.ReplaceFuturesRolloverSetAsync(pointer, [back, front]));

        var actual = (await fixture.Db.GetRolloverFuturesContractsAsync("VX"))
            .OrderBy(contract => contract.LastTradeDate)
            .ToArray();
        actual.Select(contract => (contract.ContractId, contract.OnTheRun, contract.Rollover))
            .Should().Equal(
                (front.ContractId, true, true),
                (back.ContractId, false, true));
        (await fixture.Db.GetFuturesContractRolloverAsync("VX"))!.ContractId
            .Should().Be(front.ContractId);
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

        await act.Should().ThrowAsync<OnTheRunFuturesContractNotFoundException>();
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
            },
            new()
            {
                DomainContractId = "VX20261021",
                ProviderContractName = "VX/V6",
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
        registry.TryGetOnTheRunFuturesContract("ES", out var es).Should().BeTrue();
        es.ContractId.Should().Be("ES20260918");
        registry.TryGetOnTheRunFuturesContract("VX", out var vx).Should().BeTrue();
        vx.ContractId.Should().Be("VX20260916");
        registry.TryGetFuturesTermStructureContracts("VX", out var vxTermStructure).Should().BeTrue();
        vxTermStructure.Front.ContractId.Should().Be("VX20260916");
        vxTermStructure.Back.ContractId.Should().Be("VX20261021");
    }

    private async Task ResetAsync()
    {
        foreach (var symbol in FuturesContractRolloverStartupCheck.RequiredSymbols)
        {
            var existing = await fixture.Db.GetRolloverFuturesContractsAsync(symbol);
            foreach (var contract in existing)
                await fixture.Db.DeleteFuturesContractAsync(contract.ContractId);
            await fixture.Db.Database
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractRollover)}", SecuritiesDbCql.DeleteFuturesContractRollover)
                .SetParameters(new DeleteFuturesContractRollover(symbol))
                .ExecuteCommandAsync();
        }
    }

    public async Task InitializeAsync()
    {
        foreach (var symbol in FuturesContractRolloverStartupCheck.RequiredSymbols)
        {
            var row = await fixture.Db.GetFuturesContractRolloverAsync(symbol);
            if (row is null)
                continue;
            var contracts = (await fixture.Db.GetRolloverFuturesContractsAsync(symbol))
                .ToArray();
            if (contracts.Length > 0)
                _originalAssignments.Add((row, contracts));
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
            await fixture.Db.ReplaceFuturesRolloverSetAsync(
                assignment.Rollover,
                assignment.Contracts);
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
                throw new OnTheRunFuturesContractNotFoundException(symbol, valueDate);
            var maturity = symbol == "VX"
                ? new DateOnly(2026, 9, 16)
                : valueDate >= new DateOnly(2026, 12, 18)
                    ? new DateOnly(2027, 3, 19)
                    : valueDate >= new DateOnly(2026, 9, 18)
                        ? new DateOnly(2026, 12, 18)
                        : new DateOnly(2026, 9, 18);
            var contract = new FuturesContractV3ReadModel(
                $"{symbol}{maturity:yyyyMMdd}",
                $"{symbol} integration contract",
                symbol,
                symbol == "VX"
                    ? "VX/U6"
                    : maturity.Year == 2027
                        ? "ESH7"
                        : maturity.Month == 12 ? "ESZ6" : "ESU6",
                "FUT",
                "USD",
                symbol == "VX" ? "CFE" : "CME",
                symbol == "VX" ? "1000" : "50",
                maturity,
                true);
            return Task.FromResult(new ResolvedCurrentFuturesContract(contract, maturity));
        }

        public Task<IReadOnlyList<FuturesContractV3ReadModel>> ResolveEligibleAsync(
            string symbol,
            DateOnly valueDate,
            int count,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (failVx && symbol == "VX")
                throw new OnTheRunFuturesContractNotFoundException(symbol, valueDate);
            if (symbol != "VX" || count != 2)
                throw new InvalidOperationException("The integration resolver only supplies the VX term structure.");

            IReadOnlyList<FuturesContractV3ReadModel> contracts =
                valueDate >= new DateOnly(2026, 10, 21)
                    ?
                    [
                        CreateVx(new DateOnly(2026, 11, 18), "VX/X6", true),
                        CreateVx(new DateOnly(2026, 12, 16), "VX/Z6", false)
                    ]
                    : valueDate >= new DateOnly(2026, 9, 16)
                    ?
                    [
                        CreateVx(new DateOnly(2026, 10, 21), "VX/V6", true),
                        CreateVx(new DateOnly(2026, 11, 18), "VX/X6", false)
                    ]
                    :
                    [
                        CreateVx(new DateOnly(2026, 9, 16), "VX/U6", true),
                        CreateVx(new DateOnly(2026, 10, 21), "VX/V6", false)
                    ];
            return Task.FromResult(contracts);
        }

        private static FuturesContractV3ReadModel CreateVx(
            DateOnly maturity,
            string localSymbol,
            bool onTheRun)
            => new(
                $"VX{maturity:yyyyMMdd}",
                "VX integration contract",
                "VX",
                localSymbol,
                "FUT",
                "USD",
                "CFE",
                "1000",
                maturity,
                onTheRun,
                true);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
