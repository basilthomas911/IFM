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

        methods.Should().HaveCount(27);
        methods.Select(method => method.Name).Should().BeEquivalentTo(
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
            "StopStreamingFuturesOptionChainDataAsync");
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
