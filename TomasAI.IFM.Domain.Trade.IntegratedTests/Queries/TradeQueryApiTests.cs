using TomasAI.IFM.Domain.Trade.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Queries;

public class TradeQueryApiTests(WebApplicationFactory<Program> factory, TradeDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<TradeDatabaseFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();

    [Fact]
    public async Task GetTradeHistory_Ok()
    {
        // arrange...
        var optionTrade = SampleData.CreateOptionTrade(orderId: 500, tradeId: 10);
        await dbFixture.TradeDb.DeleteOptionTradeAsync(optionTrade.OrderId, optionTrade.TradeId);
        await dbFixture.TradeDb.InsertOptionTradeAsync(optionTrade);

        // act...
        var tradeApi = new OptionTradeQueryApi(_actorProducer);
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
        var tradeApi = new OptionTradeQueryApi(_actorProducer);
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
        var tradeApi = new OptionTradeQueryApi(_actorProducer);
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
        var tradeApi = new OptionTradeQueryApi(_actorProducer);
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
        var tradeApi = new OptionTradeQueryApi(_actorProducer);
        var response = await tradeApi.GetTradePositionAsync(optionTrade.OrderId, optionTrade.TradeId, tradeType, valueDate, daysToExpiry, tradeStatus);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
    }
}
