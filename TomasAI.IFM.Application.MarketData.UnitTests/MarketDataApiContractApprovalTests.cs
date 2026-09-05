using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.UnitTests.Harness;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataApiContractApprovalTests
{
    [Fact]
    public void ApplicationInterfaceHasExpectedMethodSurfaceAndNoGuidIdentifiers()
    {
        var methods = typeof(IMarketDataApi).GetMethods();

        methods.Should().HaveCount(35);
        methods.Select(method => method.Name).Should().BeEquivalentTo(
            "GetTradeStrategySymbolsAsync",
            "IsDatabentoFeedUp",
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
            "GetFuturesPriceAsync",
            "GetFuturesOptionPriceAsync",
            "GetFuturesLastPriceReader",
            "GetFuturesOptionLastPriceReader",
            "StartStreamingFuturesTickDataAsync",
            "StopStreamingFuturesTickDataAsync",
            "StartStreamingFuturesOptionTickDataAsync",
            "StopStreamingFuturesOptionTickDataAsync",
            "StartStreamingFuturesOptionChainDataAsync",
            "StopStreamingFuturesOptionChainDataAsync",
            // Additive typed Stage 4 methods; legacy signatures remain above unchanged.
            "StartStreamingFuturesTickDataAsync",
            "StartStreamingFuturesOptionTickDataAsync",
            "StartStreamingFuturesOptionChainDataAsync",
            "RenewSubscriptionLeaseAsync",
            "ReleaseSubscriptionLeaseAsync",
            "AcquireSelectedSubscriptionLeasesAsync",
            "GetSubscriptionLeasesAsync");
        methods.SelectMany(method => method.GetParameters())
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
    public void LiveApplicationBoundaryExcludesHistoricalAcquisitionOperations()
    {
        var methods = typeof(IMarketDataApi).GetMethods();
        var historicalTerms = new[]
        {
            "Historical",
            "Batch",
            "Download",
            "EstimateCost"
        };

        methods.Should().NotContain(method => historicalTerms.Any(term =>
            method.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
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
