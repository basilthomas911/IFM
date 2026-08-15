using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoContractRegistrationRegistryTests
{
    [Fact]
    public void Rollover_replaces_matching_roots_and_preserves_explicit_unrelated_contracts()
    {
        var options = Options([
            Registration("ES20260619", "ESM6", AssetTypeId.Futures),
            Registration("NQ20260918", "NQU6", AssetTypeId.Futures),
            Registration("NQ20260918C25000", "NQU6 C25000", AssetTypeId.FuturesOption)]);
        var registry = new DatabentoContractRegistrationRegistry(options.Contracts, options);

        registry.ReplaceCurrentFuturesContracts([
            Contract("ES", "ES20260918", "ESU6", new DateOnly(2026, 9, 18)),
            Contract("VX", "VX20260916", "VXU6", new DateOnly(2026, 9, 16))]);

        registry.Should().NotContain(item => item.DomainContractId == "ES20260619");
        registry.Should().Contain(item => item.DomainContractId == "NQ20260918");
        registry.Should().Contain(item => item.DomainContractId == "NQ20260918C25000");
        registry.Should().Contain(item => item.DomainContractId == "ES20260918"
            && item.Dataset == "GLBX.MDP3");
        registry.Should().Contain(item => item.DomainContractId == "VX20260916"
            && item.Dataset == "XCBF.PITCH");
    }

    [Fact]
    public void Published_snapshots_do_not_change_during_a_later_rollover()
    {
        var options = Options([]);
        var registry = new DatabentoContractRegistrationRegistry([], options);
        registry.ReplaceCurrentFuturesContracts([
            Contract("ES", "ES20260918", "ESU6", new DateOnly(2026, 9, 18))]);
        var epochSnapshot = registry.Snapshot();

        registry.ReplaceCurrentFuturesContracts([
            Contract("ES", "ES20261218", "ESZ6", new DateOnly(2026, 12, 18))]);

        epochSnapshot.Should().ContainSingle(item => item.DomainContractId == "ES20260918");
        registry.Should().ContainSingle(item => item.DomainContractId == "ES20261218");
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

    private static FuturesContractV2ReadModel Contract(
        string symbol,
        string contractId,
        string localSymbol,
        DateOnly maturity) => new(
            contractId,
            $"{symbol} future",
            symbol,
            localSymbol,
            "FUT",
            "USD",
            symbol == "VX" ? "CFE" : "CME",
            symbol == "VX" ? "1000" : "50",
            maturity,
            true);
}
