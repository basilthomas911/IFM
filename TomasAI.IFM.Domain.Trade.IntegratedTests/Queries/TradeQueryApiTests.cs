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

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Queries;

public class TradeQueryApiTests(WebApplicationFactory<Program> factory, TradeDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<TradeDatabaseFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetTradeHistory_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 500, tradeId: 10);
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
    public async Task GetTradeLimit_Ok()
    {
        // arrange...
        var tradeLimit = new TradeLimitReadModel { TradeId = 20, TradeType = TradeType.ShortIronCondor };
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
        var tradeTypeLimit = new TradeTypeLimitReadModel { TradeId = 21, TradeType = TradeType.PutCreditSpread };
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
        var optionTrade = SampleData.CreateOptionTrade(orderId: 501, tradeId: 11);
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
    public async Task GetTradePosition_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 502, tradeId: 12);
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
}
