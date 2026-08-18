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

        methods.Should().HaveCount(23);
        methods.Select(method => method.Name).Should().BeEquivalentTo(
            "TryGetCurrentlyTradedFuturesContract",
            "TryGetLastTickPrice",
            "TryGetLastOptionTickPrice",
            "TryGetFuturesSessionStatistics",
            "IsTickDataStreamActive",
            "UpdateCurrentlyTradedFuturesContractAsync",
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

    [Theory]
    [InlineData(typeof(MarketDataApiNotRunningException))]
    [InlineData(typeof(MarketDataApiAlreadyRunningException))]
    [InlineData(typeof(MarketDataApiValueDateMismatchException))]
    [InlineData(typeof(MarketDataContractNotFoundException))]
    [InlineData(typeof(MarketDataContractKindMismatchException))]
    [InlineData(typeof(MarketDataBatchResolutionException))]
    [InlineData(typeof(MarketDataContractMappingException))]
    [InlineData(typeof(FuturesLastPriceUnavailableException))]
    [InlineData(typeof(InvalidFuturesOptionQuoteException))]
    [InlineData(typeof(TickAggregationNotRunningException))]
    [InlineData(typeof(UnderlyingTickerNotRunningException))]
    [InlineData(typeof(MarketDataRouteConflictException))]
    [InlineData(typeof(OptionChainConflictException))]
    [InlineData(typeof(MarketDataCapacityExceededException))]
    [InlineData(typeof(MarketDataPricingInputUnavailableException))]
    [InlineData(typeof(FuturesContractRolloverConfigurationException))]
    [InlineData(typeof(CurrentlyTradedFuturesContractNotFoundException))]
    public void PublicFailuresUseTypedApplicationExceptions(Type exceptionType)
    {
        exceptionType.Should().BeDerivedFrom<MarketDataApiException>();
        exceptionType.IsSealed.Should().BeTrue();
    }
}
