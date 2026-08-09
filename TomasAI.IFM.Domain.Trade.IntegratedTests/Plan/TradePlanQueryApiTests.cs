using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Plan;

public class TradePlanQueryApiTests(WebApplicationFactory<Program> factory, TradeDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<TradeDatabaseFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();

    [Fact]
    public async Task GetStopLossLimit_Ok()
    {
        // arrange...
        var tradePlan = SampleData.CreateTradePlan(orderId: 400, tradeId: 1);
        await dbFixture.TradeDb.InsertTradePlanAsync(tradePlan);

        // act...
        var tradeApi = new TradePlanQueryApi(_actorProducer);
        var response = await tradeApi.GetIronCondorStopLossLimitAsync(tradePlan.OrderId, tradePlan.TradeId);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetTradePlanForwardLossRatios_Ok()
    {
        // arrange...
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 31);
        var ratio = SampleData.CreateTradePlanForwardLossRatio(forwardLossRatio: 0.25);
        await dbFixture.TradeDb.InsertTradePlanForwardLossRatioAsync(startDate, ratio.ForwardLossRatio);

        // act...
        var tradeApi = new TradePlanQueryApi(_actorProducer);
        var response = await tradeApi.GetIronCondorTradePlanForwardLossRatiosAsync(startDate, endDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTradePlanForwardLossRatio_Ok()
    {
        // arrange...
        var valueDate = new DateOnly(2025, 1, 15);
        var ratio = SampleData.CreateTradePlanForwardLossRatio(forwardLossRatio: 0.30);
        await dbFixture.TradeDb.InsertTradePlanForwardLossRatioAsync(valueDate, ratio.ForwardLossRatio);

        // act...
        var tradeApi = new TradePlanQueryApi(_actorProducer);
        var response = await tradeApi.GetIronCondorTradePlanForwardLossRatioAsync(valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ForwardLossRatio.Should().Be(ratio.ForwardLossRatio);
    }

    [Fact]
    public async Task GetTradePlans_Ok()
    {
        // arrange...
        var tradePlan = SampleData.CreateTradePlan(orderId: 401, tradeId: 1);
        await dbFixture.TradeDb.InsertTradePlanAsync(tradePlan);

        // act...
        var tradeApi = new TradePlanQueryApi(_actorProducer);
        var response = await tradeApi.GetTradePlansAsync(tradePlan.OrderId, tradePlan.TradeId, tradePlan.ValueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value!.First().OrderId.Should().Be(tradePlan.OrderId);
        response.Value.First().TradeId.Should().Be(tradePlan.TradeId);
    }

    [Fact]
    public async Task GetIronCondorForwardDelta_Ok()
    {
        // arrange...
        var vixContractId = "VX-MAR25";
        var valueDate = new DateOnly(2025, 1, 15);
        var tradeType = TradeType.ShortIronCondor;
        var riskPositionType = RiskPositionType.Low;

        // act...
        var tradeApi = new TradePlanQueryApi(_actorProducer);
        var response = await tradeApi.GetIronCondorForwardDeltaAsync(vixContractId, valueDate, tradeType, riskPositionType);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ForwardDeltaValue.Should().Be(0.0);
    }

    [Fact]
    public async Task GetForwardLossLimitType_Ok()
    {
        // arrange...
        var forwardLossLimit = SampleData.CreateTradePlanForwardLossLimit(orderId: 402, tradeId: 1);
        await dbFixture.TradeDb.DeleteTradePlanForwardLossLimitAsync(forwardLossLimit.EntityId);
        await dbFixture.TradeDb.InsertTradePlanForwardLossLimitAsync(forwardLossLimit);

        // act...
        var tradeApi = new TradePlanQueryApi(_actorProducer);
        var response = await tradeApi.GetForwardLossLimitTypeAsync(
            forwardLossLimit.OrderId,
            forwardLossLimit.TradeId,
            forwardLossLimit.ValueDate,
            forwardLossLimit.TradeType);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.OrderId.Should().Be(forwardLossLimit.OrderId);
        response.Value.TradeId.Should().Be(forwardLossLimit.TradeId);
    }
}
