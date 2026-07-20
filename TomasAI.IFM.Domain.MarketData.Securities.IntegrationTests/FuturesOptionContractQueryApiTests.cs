using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;


namespace TomasAI.IFM.Domain.Securities.IntegrationTests;

public class FuturesOptionContractQueryApiTests(WebApplicationFactory<Program> factory, SecuritiesDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<SecuritiesDatabaseFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetFuturesOptionContract_Ok()
    {
        // arrange...
        var futuresOptionContract = SampleData.NewFuturesOptionContract;
        await dbFixture.Db.DeleteFuturesOptionContractAsync(futuresOptionContract.ContractId);
        await dbFixture.Db.InsertFuturesOptionContractAsync(futuresOptionContract);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataQueryApi(queryServiceApi);
        var response = await marketDataApi.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(futuresOptionContract.ContractId);
        response.Value.Symbol.Should().Be(futuresOptionContract.Symbol);
        response.Value.LocalSymbol.Should().Be(futuresOptionContract.LocalSymbol);
        response.Value.Description.Should().Be(futuresOptionContract.Description);
        response.Value.SecurityType.Should().Be(futuresOptionContract.SecurityType);
        response.Value.Currency.Should().Be(futuresOptionContract.Currency);
        response.Value.Exchange.Should().Be(futuresOptionContract.Exchange);
        response.Value.Multiplier.Should().Be(futuresOptionContract.Multiplier);
        response.Value.ContractMonth.Should().Be(futuresOptionContract.ContractMonth);
        response.Value.StrikePrice.Should().Be(futuresOptionContract.StrikePrice);
        response.Value.OptionType.Should().Be(futuresOptionContract.OptionType);
    }

    [Fact]
    public async Task GetFuturesOptionContracts_Ok()
    {
        // arrange...
        var futuresOptionContracts = SampleData.NewFuturesOptionContracts;
        foreach (var contract in futuresOptionContracts)
        {
            await dbFixture.Db.DeleteFuturesOptionContractAsync(contract.ContractId);
            await dbFixture.Db.InsertFuturesOptionContractAsync(contract);
        }

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataQueryApi(queryServiceApi);
        var response = await marketDataApi.GetFuturesOptionContractsAsync(futuresOptionContracts[0].Symbol);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        foreach (var expected in futuresOptionContracts)
        {
            response.Value.Should().Contain(c => c.ContractId == expected.ContractId);
        }
    }

    [Fact]
    public async Task GetFuturesOptionContractIds_Ok()
    {
        // arrange...
        var futuresOptionContracts = SampleData.NewFuturesOptionContracts;
        foreach (var contract in futuresOptionContracts)
        {
            await dbFixture.Db.DeleteFuturesOptionContractAsync(contract.ContractId);
            await dbFixture.Db.InsertFuturesOptionContractAsync(contract);
        }
        var contractIds = futuresOptionContracts.Select(c => c.ContractId).ToArray();

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataQueryApi(queryServiceApi);
        var response = await marketDataApi.GetFuturesOptionContractIdsAsync(contractIds);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        foreach (var expectedId in contractIds)
        {
            response.Value.Should().Contain(expectedId);
        }
    }
}
