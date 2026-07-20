using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class MarketDataCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task AddFuturesContract_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);

        var contract = new FuturesContractV2ReadModel(
            contractId: "TEST1",
            description: "Test Futures Contract",
            symbol: "SYM",
            localSymbol: "SYM1",
            securityType: "FUT",
            currency: "USD",
            exchange: "CME",
            multiplier: "50",
            lastTradeDate: DateOnly.FromDateTime(DateTime.Now.AddMonths(1)),
            currentlyTraded: true
        );
        var response = await marketDataApi.AddFuturesContractAsync(contract, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ChangeFuturesContract_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);

        var contractId = new FuturesContractId("TEST1", "SYM", DateOnly.FromDateTime(DateTime.Now.AddMonths(1)));
        var contract = new FuturesContractV2ReadModel(
            contractId: "TEST1",
            description: "Updated Futures Contract",
            symbol: "SYM",
            localSymbol: "SYM1",
            securityType: "FUT",
            currency: "USD",
            exchange: "CME",
            multiplier: "50",
            lastTradeDate: DateOnly.FromDateTime(DateTime.Now.AddMonths(1)),
            currentlyTraded: false
        );
        var response = await marketDataApi.ChangeFuturesContractAsync(contractId, contract, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RemoveFuturesContract_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);

        var contractId = new FuturesContractId("TEST1", "SYM", DateOnly.FromDateTime(DateTime.Now.AddMonths(1)));
        var response = await marketDataApi.RemoveFuturesContractAsync(contractId, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task AddFuturesOptionContract_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);

        var optionContract = new FuturesOptionContractReadModel(
            contractId: "SYM20251215C3456",
            description: "Test Option",
            symbol: "SYM",
            localSymbol: "SYMOPT1",
            securityType: "OPT",
            currency: "USD",
            exchange: "CME",
            multiplier: "100",
            contractMonth: DateOnly.FromDateTime(DateTime.Now.AddMonths(1)),
            strikePrice: 100.0,
            optionType: "Call"
        );
        var response = await marketDataApi.AddFuturesOptionContractAsync(optionContract, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task AddFuturesOptionContracts_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);
        var year = DateTime.UtcNow.Year;
        var optionContracts = new[]
        {
            new FuturesOptionContractReadModel(
                contractId: "SYM20251215C3456",
                description: "Test Option 2",
                symbol: "SYM",
                localSymbol: "SYMOPT2",
                securityType: "OPT",
                currency: "USD",
                exchange: "CME",
                multiplier: "100",
                contractMonth: DateOnly.FromDateTime(DateTime.Now.AddMonths(2)),
                strikePrice: 110.0,
                optionType: "Put"
            )
        };
        var response = await marketDataApi.AddFuturesOptionContractsAsync(year, optionContracts);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ChangeFuturesOptionContract_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);

        var contractId = "SYM20251215C3456";
        var optionContract = new FuturesOptionContractReadModel(
            contractId: "SYM20251215C3456",
            description: "Updated Option",
            symbol: "SYM",
            localSymbol: "SYMOPT1",
            securityType: "OPT",
            currency: "USD",
            exchange: "CME",
            multiplier: "100",
            contractMonth: DateOnly.FromDateTime(DateTime.Now.AddMonths(1)),
            strikePrice: 120.0,
            optionType: "Call"
        );
        var response = await marketDataApi.ChangeFuturesOptionContractAsync(contractId, optionContract, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RemoveFuturesOptionContract_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);

        var contractId = "SYM20251215";
        var response = await marketDataApi.RemoveFuturesOptionContractAsync(contractId, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task AddYieldCurveRate_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);

        var yieldCurveRate = new YieldCurveRateReadModel(
            valueDate: DateOnly.FromDateTime(DateTime.Now),
            oneMonth: 0.01,
            twoMonth: 0.012,
            threeMonth: 0.013,
            sixMonth: 0.014,
            oneYear: 0.015,
            twoYear: 0.016,
            threeYear: 0.017,
            fiveYear: 0.018,
            sevenYear: 0.019,
            tenYear: 0.02,
            twentyYear: 0.021,
            thirtyYear: 0.022
        );
        var response = await marketDataApi.AddYieldCurveRateAsync(yieldCurveRate, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ChangeYieldCurveRate_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);

        var yieldCurveRate = new YieldCurveRateReadModel(
            valueDate: DateOnly.FromDateTime(DateTime.Now),
            oneMonth: 0.011,
            twoMonth: 0.013,
            threeMonth: 0.014,
            sixMonth: 0.015,
            oneYear: 0.016,
            twoYear: 0.017,
            threeYear: 0.018,
            fiveYear: 0.019,
            sevenYear: 0.02,
            tenYear: 0.021,
            twentyYear: 0.022,
            thirtyYear: 0.023
        );
        var response = await marketDataApi.ChangeYieldCurveRateAsync(yieldCurveRate, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RemoveYieldCurveRate_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);

        var valueDate = DateOnly.FromDateTime(DateTime.Now);
        var response = await marketDataApi.RemoveYieldCurveRateAsync(valueDate, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ImportYieldCurveRates_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);

        var importDate = DateTime.Now;
        var yieldCurveRates = new[]
        {
            new YieldCurveRateReadModel(
                valueDate: DateOnly.FromDateTime(DateTime.Now),
                oneMonth: 0.01,
                twoMonth: 0.012,
                threeMonth: 0.013,
                sixMonth: 0.014,
                oneYear: 0.015,
                twoYear: 0.016,
                threeYear: 0.017,
                fiveYear: 0.018,
                sevenYear: 0.019,
                tenYear: 0.02,
                twentyYear: 0.021,
                thirtyYear: 0.022
            )
        };
        var response = await marketDataApi.ImportYieldCurveRatesAsync(importDate, yieldCurveRates);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
