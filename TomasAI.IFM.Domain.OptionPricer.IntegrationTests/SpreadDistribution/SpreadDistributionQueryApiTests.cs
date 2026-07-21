using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.OptionPricer.IntegrationTests.SpreadDistribution;

public class SpreadDistributionQueryApiTests(WebApplicationFactory<Program> factory, OptionPricerFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<OptionPricerFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

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
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var optionPricerQueryApi = new OptionPricerQueryApi(queryServiceApi);
        var response = await optionPricerQueryApi.GetSpreadDistributionAsync(
            SampleData.TradeId, SampleData.PutTradeType, SampleData.TradeStatus, SampleData.ValueDate, SampleData.DaysToExpiry);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.TradeId.Should().Be(SampleData.TradeId);
        response.Value.ValueDate.Should().Be(SampleData.ValueDate);
        response.Value.TradeType.Should().Be(SampleData.PutTradeType);
        response.Value.TradeStatus.Should().Be(SampleData.TradeStatus);
        response.Value.DaysToExpiry.Should().Be(SampleData.DaysToExpiry);
    }
}
