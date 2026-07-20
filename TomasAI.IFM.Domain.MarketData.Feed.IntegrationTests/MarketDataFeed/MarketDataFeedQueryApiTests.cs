using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.Trade;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.MarketDataFeed;

public class MarketDataFeedQueryApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetFuturesOptionContractQuery_Ok()
    {
        // arrange...
        var contract = SampleData.FuturesOptionContract;

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetFuturesOptionContractAsync(contract.ContractId, contract);

        // assert...
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFuturesOptionSpreadDataQuery_Ok()
    {
        // arrange...
        var shortContract = SampleData.ShortOptionContract;
        var longContract = SampleData.LongOptionContract;
        var shortTickData = SampleData.ShortOptionTickData;
        var longTickData = SampleData.LongOptionTickData;
        var valueDate = SampleData.ValueDate;
        var maturityDate = shortContract.ContractMonth;
        var assetPrice = shortTickData.UnderlyingPrice;
        var riskFreeRate = 0.045;
        var timeValue = 0.5;

        await dbFixture.MarketDataDb.DeleteFuturesOptionTickDataAsync(shortTickData.ContractId, shortTickData.ValueDate);
        await dbFixture.MarketDataDb.DeleteFuturesOptionTickDataAsync(longTickData.ContractId, longTickData.ValueDate);

        await dbFixture.MarketDataDb.InsertFuturesOptionTickDataAsync(shortTickData);
        await dbFixture.MarketDataDb.InsertFuturesOptionTickDataAsync(longTickData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetFuturesOptionSpreadDataAsync(valueDate, maturityDate, assetPrice, riskFreeRate, timeValue, shortContract, longContract);

        // assert...
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFuturesRiskPositionTypeQuery_Ok()
    {
        // arrange...
        var eodData = SampleData.FuturesEodData;
        var eodDataIndex = SampleData.FuturesEodDataIndex;
        var valueDate = SampleData.ValueDate;
        var tradeType = TradeType.ShortIronCondor;

        await dbFixture.MarketDataDb.DeleteFuturesEodDataAsync(eodData.ContractId, eodData.ValueDate);
        await dbFixture.MarketDataDb.InsertFuturesEodDataIndexAsync(eodDataIndex);
        await dbFixture.MarketDataDb.InsertFuturesEodDataAsync(eodData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetFuturesRiskPositionTypeAsync(valueDate, tradeType);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.RiskPositionType.Should().NotBe(RiskPositionType.Unknown);
    }

    [Fact]
    public async Task GetIronCondorMarketDataFeedQuery_Ok()
    {
        // arrange...
        var underlyingTickData = SampleData.UnderlyingFuturesTickData;
        var shortPutTickData = SampleData.ShortOptionTickData;
        var longPutTickData = SampleData.LongOptionTickData;
        var shortCallTickData = SampleData.ShortCallOptionTickData;
        var longCallTickData = SampleData.LongCallOptionTickData;
        var valueDate = SampleData.ValueDate;

        await dbFixture.MarketDataDb.DeleteFuturesOptionTickDataAsync(shortPutTickData.ContractId, shortPutTickData.ValueDate);
        await dbFixture.MarketDataDb.DeleteFuturesOptionTickDataAsync(longPutTickData.ContractId, longPutTickData.ValueDate);
        await dbFixture.MarketDataDb.DeleteFuturesOptionTickDataAsync(shortCallTickData.ContractId, shortCallTickData.ValueDate);
        await dbFixture.MarketDataDb.DeleteFuturesOptionTickDataAsync(longCallTickData.ContractId, longCallTickData.ValueDate);

        await dbFixture.MarketDataDb.InsertFuturesTickDataAsync(underlyingTickData);
        await dbFixture.MarketDataDb.InsertFuturesOptionTickDataAsync(shortPutTickData);
        await dbFixture.MarketDataDb.InsertFuturesOptionTickDataAsync(longPutTickData);
        await dbFixture.MarketDataDb.InsertFuturesOptionTickDataAsync(shortCallTickData);
        await dbFixture.MarketDataDb.InsertFuturesOptionTickDataAsync(longCallTickData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetIronCondorMarketDataFeedAsync(
            underlyingTickData.ContractId,
            shortPutTickData.ContractId,
            longPutTickData.ContractId,
            shortCallTickData.ContractId,
            longCallTickData.ContractId,
            valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.AssetPrice.Should().Be(underlyingTickData.Price);
        response.Value.ShortPutOptionData.Should().NotBeNull();
        response.Value.ShortPutOptionData.ContractId.Should().Be(shortPutTickData.ContractId);
        response.Value.LongPutOptionData.Should().NotBeNull(); 
        response.Value.LongPutOptionData.ContractId.Should().Be(longPutTickData.ContractId);
        response.Value.ShortCallOptionData.Should().NotBeNull();
        response.Value.ShortCallOptionData.ContractId.Should().Be(shortCallTickData.ContractId);
        response.Value.LongCallOptionData.Should().NotBeNull();
        response.Value.LongCallOptionData.ContractId.Should().Be(longCallTickData.ContractId);
    }

    [Fact]
    public async Task GetNormalCurveTableQuery_Ok()
    {
        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetNormalCurveTableAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.NormalCurveTable.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOptionQuoteIdQuery_Ok()
    {
        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetOptionQuoteIdAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.AsInteger.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetStreamingRequestIdQuery_Ok()
    {
        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetStreamingRequestIdAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.AsInteger.Should().BeGreaterThan(0);
    }
}