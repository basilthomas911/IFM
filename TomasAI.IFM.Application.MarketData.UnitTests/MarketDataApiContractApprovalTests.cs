using FluentAssertions;
using System.Reflection;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.UnitTests.Harness;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataApiContractApprovalTests
{
    [Fact]
    public void ApplicationInterfaceHasExpectedMethodSurfaceAndCanonicalLegacyIdentifiers()
    {
        var methods = typeof(IMarketDataApi).GetMethods();

        methods.Should().HaveCount(76);
        methods.Select(method => method.Name).Should().BeEquivalentTo(
            "get_DatasetRecoveryOwnership",
            "InjectDevelopmentOptionWorkerFaultAsync", "WarmTreasuryPricingAsync", "ReadTreasuryCurveAsync",
            "ReadTreasuryCurvesAsync", "ReadEconomicCalendarAsync", "RefreshInstrumentDefinitionsAsync",
            "RefreshOptionCalendarAsync", "EvaluateOptionChainAsync", "ResetMarketDataAsync",
            "ReconcileCommittedSubscriptionsAsync", "ReleaseCompositionAsync", "AcquireMonitoredOptionAsync",
            "ReleaseMonitoredOptionAsync", "ReadFiveMinuteSeedBarsAsync", "ReadIndicatorSeedBarsAsync", "GetBackendReadSnapshotAsync",
            "PrepareSessionAsync", "GetBackendStatusAsync", "RecoverMarketDataAsync", "RepairOptionObservationsAsync",
            "AcquireOptionScopeAsync", "ReleaseOptionScopeAsync", "CaptureCompositionAsync", "CaptureOptionRiskAsync",
            "PrepareCompositionAsync", "PrepareCachedCompositionAsync", "GetHistoricalAvailableEndAsync",
            "EstimateHistoricalAsync", "LoadHistoricalAsync", "CloseTradeFeedRecoveryAsync", "ReadTradeFeedRecoveryAsync",
            "LoadTradeFeedRecoveryAsync", "EnableTradeFeedRecoveryAsync", "DisableTradeFeedRecoveryAsync",
            "get_OptionChainCache",
            "TryGetMarketInstrumentId",
            "GetFuturesMarketHealth",
            "GetTradeStrategySymbolsAsync",
            "IsDatabentoFeedUp",
            "get_CoreFuturesRoutesAreRuntimeOwned",
            "GetRuntimeStatus",
            "TryGetOnTheRunFuturesContract",
            "TryGetFuturesTermStructureContracts",
            "UpdateFuturesTermStructureContractsAsync",
            "TryGetLastTickPrice",
            "TryGetLastOptionTickPrice",
            "TryGetFuturesSessionStatistics",
            "IsTickDataStreamActive",
            "UpdateOnTheRunFuturesContractAsync",
            "StartAsync",
            "StopAsync",
            "GetFuturesContractAsync",
            "GetFuturesContractsAsync",
            "GetFuturesOptionContractAsync",
            "GetFuturesOptionContractsAsync",
            "GetFuturesOptionChainContractsAsync",
            "GetFuturesOptionChainContractsBySymbolAsync",
            "DiscoverOptionContractExpiriesAsync",
            "GetFuturesOptionChainContractsByRootAsync",
            "GetFuturesOptionChainContractsByRootAsync",
            "GetFuturesPriceAsync",
            "GetFuturesOptionPriceAsync",
            "GetFuturesLastPriceReader",
            "GetFuturesOptionLastPriceReader",
            "StartStreamingFuturesTickDataAsync",
            "StopStreamingFuturesTickDataAsync",
            "StartStreamingFuturesOptionTickDataAsync",
            "StopStreamingFuturesOptionTickDataAsync",
            // Additive typed Stage 4 methods; legacy signatures remain above unchanged.
            "StartStreamingFuturesTickDataAsync",
            "StartStreamingFuturesOptionTickDataAsync",
            "StartStreamingFuturesOptionChainDataAsync",
            "RenewSubscriptionLeaseAsync",
            "ReleaseSubscriptionLeaseAsync",
            "AcquireSelectedSubscriptionLeasesAsync",
            "GetSubscriptionLeasesAsync");
        // Backend operations carry operation/generation identities; canonical legacy contract IDs remain strings.
        methods.Where(method => method.GetCustomAttribute<MarketDataHostOperationAttribute>() is null)
            .SelectMany(method => method.GetParameters())
            .Should().NotContain(parameter => parameter.ParameterType == typeof(Guid));
    }

    [Fact]
    public void HarnessImplementsTheSoleApplicationMarketDataContract()
    {
        var context = new MarketDataApiTestContext();

        context.Api.Should().BeAssignableTo<IMarketDataApi>();
        typeof(IMarketDataApi).Assembly.GetType(
                $"{typeof(IMarketDataApi).Namespace}.IMarketDataSnapshotApi")
            .Should().BeNull();
    }

    [Fact]
    public async Task Typed_lease_methods_fail_closed_without_starting_a_legacy_epoch()
    {
        var context = new MarketDataApiTestContext();
        IMarketDataApi api = context.Api;
        var request = new SubscriptionAcquireRequest(Guid.CreateVersion7(), Guid.NewGuid(), Guid.NewGuid(),
            new("test", new("workflow", "one", "leg")),
            new(new SubscriptionTickerKey("databento", "GLBX.MDP3", "ES", "mbp-1", SubscriptionAssetKind.Futures)),
            SubscriptionLeasePurpose.Position, DateTimeOffset.UtcNow.AddSeconds(10));
        (await api.StartStreamingFuturesTickDataAsync(request)).Code.Should().Be(SubscriptionResultCode.Disabled);
        (await api.StartStreamingFuturesOptionTickDataAsync(request)).Code.Should().Be(SubscriptionResultCode.Disabled);
        (await api.StartStreamingFuturesOptionChainDataAsync(request)).Code.Should().Be(SubscriptionResultCode.Disabled);
        (await api.AcquireSelectedSubscriptionLeasesAsync(new(request.OperationId, request.HostEpochId,
            request.CorrelationId, request.Owner, [new(request.Owner, request.Target)], request.Purpose, request.DeadlineUtc)))
            .Code.Should().Be(SubscriptionResultCode.Disabled);
        (await api.RenewSubscriptionLeaseAsync(new(request.OperationId, request.CorrelationId,
            request.Owner, new(Guid.NewGuid(), Guid.NewGuid(), 1), request.DeadlineUtc))).Code.Should().Be(SubscriptionResultCode.Disabled);
        (await api.ReleaseSubscriptionLeaseAsync(new(request.OperationId, request.CorrelationId,
            request.Owner, new(Guid.NewGuid(), Guid.NewGuid(), 1), request.DeadlineUtc))).Code.Should().Be(SubscriptionResultCode.Disabled);
        (await api.GetSubscriptionLeasesAsync(new(request.Owner))).Code.Should().Be(SubscriptionResultCode.Disabled);
        context.EpochFactory.Epochs.Should().BeEmpty();
    }

    [Fact]
    public void HistoricalAcquisitionIsExplicitlyMarkedAsTypedBackendOperations()
    {
        var methods = typeof(IMarketDataApi).GetMethods();
        var historicalTerms = new[]
        {
            "Historical",
            "Batch",
            "Download",
            "EstimateCost"
        };

        var historical = methods.Where(method => historicalTerms.Any(term =>
            method.Name.Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray();
        historical.Select(method => method.Name).Should().BeEquivalentTo(
            "GetHistoricalAvailableEndAsync", "EstimateHistoricalAsync", "LoadHistoricalAsync");
        foreach (var method in historical)
        {
            method.GetCustomAttribute<MarketDataHostOperationAttribute>().Should().NotBeNull();
            method.GetParameters().Select(parameter => parameter.ParameterType).Should()
                .Equal(typeof(MarketDataHistoricalRequest), typeof(CancellationToken));
        }
        methods.SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType.FullName ?? string.Empty)
            .Should().NotContain(typeName =>
                typeName.Contains("DataBento", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(typeof(MarketDataApiNotRunningException))]
    [InlineData(typeof(MarketDataApiAlreadyRunningException))]
    [InlineData(typeof(MarketDataApiValueDateMismatchException))]
    [InlineData(typeof(MarketDataContractNotFoundException))]
    [InlineData(typeof(MarketDataContractKindMismatchException))]
    [InlineData(typeof(MarketDataBatchResolutionException))]
    [InlineData(typeof(MarketDataContractMappingException))]
    [InlineData(typeof(InvalidFuturesOptionQuoteException))]
    [InlineData(typeof(TickAggregationNotRunningException))]
    [InlineData(typeof(UnderlyingTickerNotRunningException))]
    [InlineData(typeof(MarketDataRouteConflictException))]
    [InlineData(typeof(OptionChainConflictException))]
    [InlineData(typeof(MarketDataCapacityExceededException))]
    [InlineData(typeof(MarketDataPricingInputUnavailableException))]
    [InlineData(typeof(FuturesContractRolloverConfigurationException))]
    [InlineData(typeof(OnTheRunFuturesContractNotFoundException))]
    public void PublicFailuresUseTypedApplicationExceptions(Type exceptionType)
    {
        exceptionType.Should().BeDerivedFrom<MarketDataApiException>();
        exceptionType.IsSealed.Should().BeTrue();
    }
}
