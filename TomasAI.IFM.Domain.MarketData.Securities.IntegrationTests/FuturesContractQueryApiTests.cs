using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests;

public class FuturesContractQueryApiTests(WebApplicationFactory<Program> factory, SecuritiesDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<SecuritiesDatabaseFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetCurrentlyTradedFuturesContract_Ok()
    {
        // arrange...
        var futuresContract = SampleData.NewFuturesContract with { CurrentlyTraded = true};
        await dbFixture.Db.DeleteCurrentlyTradedFuturesContractAsync(futuresContract.Symbol);
        await dbFixture.Db.InsertFuturesContractAsync(futuresContract);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataQueryApi(queryServiceApi);
        var response = await marketDataApi.GetCurrentlyTradedFuturesContractAsync(SampleData.NewFuturesContract.Symbol);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(futuresContract.ContractId);
        response.Value.Symbol.Should().Be(futuresContract.Symbol);
        response.Value.LocalSymbol.Should().Be(futuresContract.LocalSymbol);
        response.Value.Description.Should().Be(futuresContract.Description);
        response.Value.SecurityType.Should().Be(futuresContract.SecurityType);
        response.Value.Currency.Should().Be(futuresContract.Currency);
        response.Value.Exchange.Should().Be(futuresContract.Exchange);
        response.Value.Multiplier.Should().Be(futuresContract.Multiplier);
        response.Value.LastTradeDate.Should().Be(futuresContract.LastTradeDate);
        response.Value.CurrentlyTraded.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentlyTradedFuturesContracts_Ok()
    {
        // arrange...
        var futuresContract = SampleData.NewFuturesContract with { CurrentlyTraded = true };
        await dbFixture.Db.DeleteCurrentlyTradedFuturesContractAsync(futuresContract.Symbol);
        await dbFixture.Db.InsertFuturesContractAsync(futuresContract);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataQueryApi(queryServiceApi);
        var response = await marketDataApi.GetCurrentlyTradedFuturesContractsAsync(SampleData.NewFuturesContract.Symbol);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(c => c.ContractId == futuresContract.ContractId && c.CurrentlyTraded);
    }

    [Fact]
    public async Task GetFuturesContract_Ok()
    {
        // arrange...
        var futuresContract = SampleData.NewFuturesContract;
        await dbFixture.Db.DeleteFuturesContractAsync(futuresContract.Id);
        await dbFixture.Db.InsertFuturesContractAsync(futuresContract);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataQueryApi(queryServiceApi);
        var response = await marketDataApi.GetFuturesContractAsync(futuresContract.ContractId);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(futuresContract.ContractId);
        response.Value.Symbol.Should().Be(futuresContract.Symbol);
        response.Value.LocalSymbol.Should().Be(futuresContract.LocalSymbol);
        response.Value.Description.Should().Be(futuresContract.Description);
        response.Value.SecurityType.Should().Be(futuresContract.SecurityType);
        response.Value.Currency.Should().Be(futuresContract.Currency);
        response.Value.Exchange.Should().Be(futuresContract.Exchange);
        response.Value.Multiplier.Should().Be(futuresContract.Multiplier);
        response.Value.LastTradeDate.Should().Be(futuresContract.LastTradeDate);
        response.Value.CurrentlyTraded.Should().Be(futuresContract.CurrentlyTraded);
    }

    [Fact]
    public async Task GetFuturesContracts_Ok()
    {
        // arrange...
        var futuresContract = SampleData.NewFuturesContract;
        await dbFixture.Db.DeleteFuturesContractAsync(futuresContract.Id);
        await dbFixture.Db.InsertFuturesContractAsync(futuresContract);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataQueryApi(queryServiceApi);
        var response = await marketDataApi.GetFuturesContractsAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(c => c.ContractId == futuresContract.ContractId);
    }
}
