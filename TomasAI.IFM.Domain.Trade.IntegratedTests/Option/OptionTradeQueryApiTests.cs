using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Option;

public class OptionTradeQueryApiTests(WebApplicationFactory<Program> factory, TradeDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<TradeDatabaseFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetOptionTrade_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 200, tradeId: 1);
        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.OrderId.Should().Be(optionTrade.OrderId);
        response.Value.TradeId.Should().Be(optionTrade.TradeId);
    }

    [Fact]
    public async Task GetOptionTrades_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 201, tradeId: 1);
        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetOptionTradesAsync(optionTrade.OrderId);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTradeLimit_Ok()
    {
        // arrange...
        var tradeLimit = new TradeLimitReadModel { TradeId = 2, TradeType = TradeType.ShortIronCondor };
        await dbFixture.TradeDb.DeleteTradeLimitAsync(tradeLimit.TradeId, tradeLimit.TradeType);
        await dbFixture.TradeDb.InsertTradeLimitAsync(tradeLimit);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetTradeLimitAsync(tradeLimit.TradeId);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.TradeId.Should().Be(tradeLimit.TradeId);
    }

    [Fact]
    public async Task GetTradeTypeLimit_Ok()
    {
        // arrange...
        var tradeTypeLimit = new TradeTypeLimitReadModel { TradeId = 3, TradeType = TradeType.PutCreditSpread };
        await dbFixture.TradeDb.DeleteTradeTypeLimitAsync(tradeTypeLimit.TradeId);
        await dbFixture.TradeDb.InsertTradeTypeLimitAsync(tradeTypeLimit);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetTradeTypeLimitAsync(tradeTypeLimit.TradeId, tradeTypeLimit.TradeType);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.TradeId.Should().Be(tradeTypeLimit.TradeId);
        response.Value.TradeType.Should().Be(tradeTypeLimit.TradeType);
    }

    [Fact]
    public async Task GetTradeQuantity_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 203, tradeId: 4);
        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetTradeQuantityAsync(optionTrade.TradeId);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTradeHistory_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 204, tradeId: 5);
        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetTradeHistoryAsync(optionTrade.OrderId);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOptionLegContractIds_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 205, tradeId: 6);
        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetOptionLegContractIdsAsync(optionTrade.TradeId);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTradePositions_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 206, tradeId: 7);
        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetTradePositionsAsync(optionTrade.OrderId, optionTrade.TradeId);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTradePosition_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 207, tradeId: 8);
        var valueDate = new DateOnly(2025, 1, 15);
        var tradeType = TradeType.PutCreditSpread;
        var daysToExpiry = 65;
        var tradeStatus = TradeStatus.Open;

        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetTradePositionAsync(optionTrade.OrderId, optionTrade.TradeId, tradeType, valueDate, daysToExpiry, tradeStatus);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetIronCondorTradePrice_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 208, tradeId: 9);
        var valueDate = new DateOnly(2025, 1, 15);

        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetIronCondorTradePriceAsync(optionTrade.TradeId, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetTradePlanSummary_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 209, tradeId: 10);
        var valueDate = new DateOnly(2025, 1, 15);

        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetTradePlanSummaryAsync( optionTrade.OrderId, optionTrade.TradeId, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTradePositionTradeTypes_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 210, tradeId: 11);
        var valueDate = new DateOnly(2025, 1, 15);
        var daysToExpiry = 65;
        var tradeStatus = TradeStatus.Open;

        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetTradePositionTradeTypesAsync(optionTrade.OrderId, optionTrade.TradeId, valueDate, daysToExpiry, tradeStatus);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetIronCondorMDILimit_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 211, tradeId: 12);
        var valueDate = new DateOnly(2025, 1, 15);

        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetIronCondorMDILimitAsync(optionTrade.OrderId, optionTrade.TradeId, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetOptionTradeSpreadData_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 212, tradeId: 13);
        var valueDate = new DateOnly(2025, 1, 15);
        var tradeType = TradeType.PutCreditSpread;

        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        var spreadData = new OptionTradeSpreadsDataModel
        {
            OrderId = optionTrade.OrderId,
            TradeId = optionTrade.TradeId,
            ValueDate = valueDate,
            TradeType = tradeType
        };
        await dbFixture.TradeDb.DeleteOptionTradeSpreadDataAsync(optionTrade.OrderId, optionTrade.TradeId, valueDate, tradeType);
        await dbFixture.TradeDb.InsertOptionTradeSpreadDataAsync(spreadData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetOptionTradeSpreadDataAsync(optionTrade.OrderId, optionTrade.TradeId, tradeType, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.OrderId.Should().Be(optionTrade.OrderId);
        response.Value.TradeId.Should().Be(optionTrade.TradeId);
    }

    [Fact]
    public async Task GetOptionTradeSpreadBarData_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 213, tradeId: 14);
        var valueDate = new DateOnly(2025, 1, 15);
        var tradeType = TradeType.PutCreditSpread;
        var startDate = new DateTime(2025, 1, 15, 9, 0, 0);
        var endDate = new DateTime(2025, 1, 15, 16, 0, 0);

        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        var spreadBarData = new OptionTradeSpreadBarsDataModel
        {
            OrderId = optionTrade.OrderId,
            TradeId = optionTrade.TradeId,
            ValueDate = valueDate,
            TradeType = tradeType
        };
        await dbFixture.TradeDb.DeleteOptionTradeSpreadBarDataAsync(optionTrade.OrderId, optionTrade.TradeId, valueDate, tradeType);
        await dbFixture.TradeDb.InsertOptionTradeSpreadBarDataAsync(spreadBarData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var tradeApi = new OptionTradeQueryApi(queryServiceApi);
        var response = await tradeApi.GetOptionTradeSpreadBarDataAsync(optionTrade.OrderId, optionTrade.TradeId, tradeType, valueDate, startDate, endDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }
}
