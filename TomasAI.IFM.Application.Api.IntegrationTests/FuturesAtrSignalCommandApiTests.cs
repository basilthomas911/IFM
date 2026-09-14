using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class FuturesAtrSignalCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task GenerateFuturesAtrSignal_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var atrSignalId = new FuturesAtrSignalId(
            "CONTRACT1",
            DateOnly.FromDateTime(DateTime.Now),
            TimeFrameType.FifteenSeconds,
            14,
            TimeOnly.FromDateTime(DateTime.Now));
        var observation = new FuturesTradeSessionBarReadModel
        {
            ContractId = atrSignalId.ContractId,
            ValueDate = atrSignalId.ValueDate,
            TimeFrame = atrSignalId.TimePeriod,
            Close = 100m,
            IsComplete = true,
            IsValid = true
        };
        var response = await api.GenerateFuturesAtrSignalAsync(atrSignalId, observation);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
