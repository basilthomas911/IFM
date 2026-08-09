using TomasAI.IFM.Domain.Trade.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.OptionPricer.IntegrationTests.SpreadDistribution;

public class SpreadDistributionQueryApiTests(WebApplicationFactory<Program> factory, OptionPricerFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<OptionPricerFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();

    async Task SeedSpreadDistributionAsync()
    {
        await dbFixture.OptionPricerDb.DeleteSpreadDistributionAsync(SampleData.TradeId, SampleData.ValueDate);
        await dbFixture.OptionPricerDb.InsertSpreadDistributionsAsync(SampleData.PutSpreadDistribution, SampleData.CallSpreadDistribution);
    }

    [Fact]
    public async Task GetSpreadDistribution_Ok()
    {
        // arrange...
        await SeedSpreadDistributionAsync();

        // act...
        var optionPricerQueryApi = new OptionPricerQueryApi(_actorProducer);
        var response = await optionPricerQueryApi.GetSpreadDistributionAsync(
            SampleData.TradeId, SampleData.PutTradeType, SampleData.TradeStatus, SampleData.ValueDate, SampleData.DaysToExpiry);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBeNull();
        response.Value!.TradeId.Should().Be(SampleData.TradeId);
        response.Value.ValueDate.Should().Be(SampleData.ValueDate);
        response.Value.TradeType.Should().Be(SampleData.PutTradeType);
        response.Value.TradeStatus.Should().Be(SampleData.TradeStatus);
        response.Value.DaysToExpiry.Should().Be(SampleData.DaysToExpiry);
    }
}
