using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoContractRegistrationRegistryTests
{
    [Fact]
    public void TermStructurePublishesOrderedFrontBackContractsAndRegistersBoth()
    {
        var options = Options([]);
        var registry = new DatabentoContractRegistrationRegistry([], options);
        var pair = new FuturesTermStructureContracts(
            Contract("VX", "VX20260916", "VXU6", new DateOnly(2026, 9, 16)),
            Contract("VX", "VX20261021", "VXV6", new DateOnly(2026, 10, 21), false));

        registry.ReplaceFuturesRolloverSet("VX", [pair.Front, pair.Back]);

        registry.TryGetFuturesTermStructureContracts("vx", out var actual).Should().BeTrue();
        actual.Should().Be(pair);
        registry.TryGetOnTheRunFuturesContract("VX", out var current).Should().BeTrue();
        current.Should().Be(pair.Front);
        registry.Should().Contain(item => item.DomainContractId == pair.Front.ContractId);
        registry.Should().Contain(item => item.DomainContractId == pair.Back.ContractId);
    }

    [Fact]
    public void Rollover_replaces_matching_roots_and_preserves_explicit_unrelated_contracts()
    {
        var options = Options([
            Registration("ES20260619", "ESM6", AssetTypeId.Futures),
            Registration("NQ20260918", "NQU6", AssetTypeId.Futures),
            Registration("NQ20260918C25000", "NQU6 C25000", AssetTypeId.FuturesOption)]);
        var registry = new DatabentoContractRegistrationRegistry(options.Contracts, options);

        registry.ReplaceFuturesRolloverSet("ES", [
            Contract("ES", "ES20260918", "ESU6", new DateOnly(2026, 9, 18))]);
        registry.ReplaceFuturesRolloverSet("VX", [
            Contract("VX", "VX20260916", "VXU6", new DateOnly(2026, 9, 16)),
            Contract("VX", "VX20261021", "VXV6", new DateOnly(2026, 10, 21), false)]);

        registry.Should().NotContain(item => item.DomainContractId == "ES20260619");
        registry.Should().Contain(item => item.DomainContractId == "NQ20260918");
        registry.Should().Contain(item => item.DomainContractId == "NQ20260918C25000");
        registry.Should().Contain(item => item.DomainContractId == "ES20260918"
            && item.Dataset == "GLBX.MDP3");
        registry.Should().Contain(item => item.DomainContractId == "VX20260916"
            && item.Dataset == "XCBF.PITCH");
        registry.TryGetOnTheRunFuturesContract("es", out var currentEs)
            .Should().BeTrue();
        currentEs.ContractId.Should().Be("ES20260918");
        registry.TryGetOnTheRunFuturesContract("VX", out var currentVx)
            .Should().BeTrue();
        currentVx.ContractId.Should().Be("VX20260916");
    }

    [Fact]
    public void Published_snapshots_do_not_change_during_a_later_rollover()
    {
        var options = Options([]);
        var registry = new DatabentoContractRegistrationRegistry([], options);
        registry.ReplaceFuturesRolloverSet("ES", [
            Contract("ES", "ES20260918", "ESU6", new DateOnly(2026, 9, 18))]);
        var epochSnapshot = registry.Snapshot();

        registry.ReplaceFuturesRolloverSet("ES", [
            Contract("ES", "ES20261218", "ESZ6", new DateOnly(2026, 12, 18))]);

        epochSnapshot.Should().ContainSingle(item => item.DomainContractId == "ES20260918");
        registry.Should().ContainSingle(item => item.DomainContractId == "ES20261218");
        registry.TryGetOnTheRunFuturesContract("ES", out var current)
            .Should().BeTrue();
        current.ContractId.Should().Be("ES20261218");
    }

    [Fact]
    public void RejectsVxPairWithTwoOnTheRunContracts()
    {
        var registry = new DatabentoContractRegistrationRegistry([], Options([]));
        var contracts = new[]
        {
            Contract("VX", "VX20260916", "VXU6", new DateOnly(2026, 9, 16)),
            Contract("VX", "VX20261021", "VXV6", new DateOnly(2026, 10, 21))
        };

        var act = () => registry.ReplaceFuturesRolloverSet("VX", contracts);

        act.Should().Throw<ArgumentException>();
        registry.Should().BeEmpty();
    }

    [Fact]
    public void RejectsOnTheRunContractOutsideRolloverSet()
    {
        var registry = new DatabentoContractRegistrationRegistry([], Options([]));
        var invalid = Contract(
            "ES", "ES20260918", "ESU6", new DateOnly(2026, 9, 18)) with
        {
            Rollover = false
        };

        var act = () => registry.ReplaceFuturesRolloverSet("ES", [invalid]);

        act.Should().Throw<ArgumentException>();
        registry.Should().BeEmpty();
    }

    private static DatabentoMarketDataRuntimeOptions Options(
        IReadOnlyList<DatabentoContractRegistration> registrations) => new()
    {
        FeedOptions = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
        Contracts = registrations
    };

    private static DatabentoContractRegistration Registration(
        string domainId,
        string providerId,
        AssetTypeId assetTypeId) => new()
    {
        DomainContractId = domainId,
        ProviderContractName = providerId,
        AssetTypeId = assetTypeId
    };

    private static FuturesContractV3ReadModel Contract(
        string symbol,
        string contractId,
        string localSymbol,
        DateOnly maturity,
        bool onTheRun = true) => new(
            contractId,
            $"{symbol} future",
            symbol,
            localSymbol,
            "FUT",
            "USD",
            symbol == "VX" ? "CFE" : "CME",
            symbol == "VX" ? "1000" : "50",
            maturity,
            onTheRun,
            true);
}
