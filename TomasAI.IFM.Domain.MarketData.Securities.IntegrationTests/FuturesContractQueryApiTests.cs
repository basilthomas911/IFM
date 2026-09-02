using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests;

public class FuturesContractQueryApiTests(WebApplicationFactory<Program> factory, SecuritiesDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<SecuritiesDatabaseFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetOnTheRunFuturesContract_Ok()
    {
        // arrange...
        var futuresContract = SampleData.NewFuturesContract with { OnTheRun = true};
        await dbFixture.Db.DeleteOnTheRunFuturesContractAsync(futuresContract.Symbol);
        await dbFixture.Db.InsertFuturesContractAsync(futuresContract);

        // act...
        var marketDataApi = new MarketDataQueryApi(_actorProducer);
        var response = await marketDataApi.GetOnTheRunFuturesContractAsync(SampleData.NewFuturesContract.Symbol);

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
        response.Value.OnTheRun.Should().BeTrue();
    }

    [Fact]
    public async Task GetRolloverFuturesContracts_Ok()
    {
        // arrange...
        var futuresContract = SampleData.NewFuturesContract with { OnTheRun = true };
        await dbFixture.Db.DeleteOnTheRunFuturesContractAsync(futuresContract.Symbol);
        await dbFixture.Db.InsertFuturesContractAsync(futuresContract);

        // act...
        var marketDataApi = new MarketDataQueryApi(_actorProducer);
        var response = await marketDataApi.GetRolloverFuturesContractsAsync(SampleData.NewFuturesContract.Symbol);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(c => c.ContractId == futuresContract.ContractId && c.OnTheRun);
    }

    [Fact]
    public async Task GetFuturesContract_Ok()
    {
        // arrange...
        var futuresContract = SampleData.NewFuturesContract;
        await dbFixture.Db.DeleteFuturesContractAsync(futuresContract.Id);
        await dbFixture.Db.InsertFuturesContractAsync(futuresContract);

        // act...
        var marketDataApi = new MarketDataQueryApi(_actorProducer);
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
        response.Value.OnTheRun.Should().Be(futuresContract.OnTheRun);
    }

    [Fact]
    public async Task GetFuturesContracts_Ok()
    {
        // arrange...
        var futuresContract = SampleData.NewFuturesContract;
        await dbFixture.Db.DeleteFuturesContractAsync(futuresContract.Id);
        await dbFixture.Db.InsertFuturesContractAsync(futuresContract);

        // act...
        var marketDataApi = new MarketDataQueryApi(_actorProducer);
        var response = await marketDataApi.GetFuturesContractsAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(c => c.ContractId == futuresContract.ContractId);
    }
}
