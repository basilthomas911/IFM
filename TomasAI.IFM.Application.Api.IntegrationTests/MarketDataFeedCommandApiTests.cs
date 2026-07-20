using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class MarketDataFeedCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task StartMarketDataFeed_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var contracts = new[]
        {
            new FuturesContractV2ReadModel("TEST1", "desc", "SYM", "SYM1", "FUT", "USD", "CME", "50", DateOnly.FromDateTime(DateTime.Now), true)
        };
        var response = await api.StartMarketDataFeedAsync(contracts, DateOnly.FromDateTime(DateTime.Now));
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StopMarketDataFeed_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var response = await api.StopMarketDataFeedAsync(DateOnly.FromDateTime(DateTime.Now));
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ResetMarketDataFeed_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var contracts = new[]
        {
            new FuturesContractV2ReadModel("TEST1", "desc", "SYM", "SYM1", "FUT", "USD", "CME", "50", DateOnly.FromDateTime(DateTime.Now), true)
        };
        var response = await api.ResetMarketDataFeedAsync(contracts, DateOnly.FromDateTime(DateTime.Now));
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task InsertFuturesTickData_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var contract = new FuturesContractV2ReadModel("TEST1", "desc", "SYM", "SYM1", "FUT", "USD", "CME", "50", DateOnly.FromDateTime(DateTime.Now), true);
        var tickData = new FuturesTickDataV2ReadModel("TEST1", DateOnly.FromDateTime(DateTime.Now), 1L, TimeOnly.MinValue, 1m, 1);
        var response = await api.InsertFuturesTickDataAsync(contract, tickData);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task InsertFuturesOptionTickData_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var contract = new FuturesContractV2ReadModel("SYM20251010C3456", "desc", "SYM", "SYM1", "FUT", "USD", "CME", "50", DateOnly.FromDateTime(DateTime.Now), true);
        var optionTickData = new FuturesOptionTickDataV2ReadModel("SYM20251010C3456", DateOnly.FromDateTime(DateTime.Now), 1L, TimeOnly.MinValue, 1.0, 1.0, 1.0, 1, 1, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0);
        var response = await api.InsertFuturesOptionTickDataAsync(contract, optionTickData);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StartFuturesOptionTickDataStreaming_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var feedId = new FeedId(1);
        var optionContract = new FuturesOptionContractReadModel("SYM20251010C3456", "SYM", "SYM1", "FUT", "USD", "CME", "50", "OPT", DateOnly.FromDateTime(DateTime.Now), 1.0, "CALL");
        var baseContract = new FuturesContractV2ReadModel("SYM20251010C3456", "desc", "SYM", "SYM1", "FUT", "USD", "CME", "50", DateOnly.FromDateTime(DateTime.Now), true);
        var valueDate = DateOnly.FromDateTime(DateTime.Now);
        var entityId = new FuturesOptionTickEntityId(optionContract.ContractId, valueDate);
        var maturityDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30));
        var riskFreeRate = 0.01;
        var response = await api.StartFuturesOptionTickDataStreamingAsync(entityId, optionContract, baseContract, valueDate, maturityDate, riskFreeRate);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StopFuturesOptionTickDataStreaming_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var feedId = new FeedId(1);
        var contractId = "TEST1";
        var valueDate = DateOnly.FromDateTime(DateTime.Now);
        var entityId = new FuturesOptionTickEntityId(contractId, valueDate);
        var response = await api.StopFuturesOptionTickDataStreamingAsync(entityId, contractId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeleteStreamingRequestId_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var feedId = new FeedId(1);
        var response = await api.DeleteStreamingRequestIdAsync(feedId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StartFuturesTickDataStreaming_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var contract = new FuturesContractV2ReadModel("TEST1", "desc", "SYM", "SYM1", "FUT", "USD", "CME", "50", DateOnly.FromDateTime(DateTime.Now), true);
        var valueDate = DateOnly.FromDateTime(DateTime.Now);
        var resetStream = false;
        var response = await api.StartFuturesTickDataStreamingAsync(contract, valueDate, resetStream);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StopFuturesTickDataStreaming_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var contractId = "TEST1";
        var response = await api.StopFuturesTickDataStreamingAsync(contractId, DateOnly.FromDateTime(DateTime.Now));
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StartFuturesBarDataStreaming_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var contracts = new[] { new FuturesContractV2ReadModel("TEST1", "desc", "SYM", "SYM1", "FUT", "USD", "CME", "50", DateOnly.FromDateTime(DateTime.Now), true) };
        var valueDate = DateOnly.FromDateTime(DateTime.Now);
        var response = await api.StartFuturesBarDataStreamingAsync(contracts, valueDate);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StopFuturesBarDataStreaming_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var valueDate = DateOnly.FromDateTime(DateTime.Now);
        var response = await api.StopFuturesBarDataStreamingAsync(valueDate);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task EnableTradeLiveFeed_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var orderId = 1;
        var tradeId = 1;
        var response = await api.EnableTradeLiveFeedAsync(orderId, tradeId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task DisableTradeLiveFeed_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var orderId = 1;
        var tradeId = 1;
        var response = await api.DisableTradeLiveFeedAsync(orderId, tradeId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task AddTradeLiveFeed_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var orderId = 1;
        var tradeId = 1;
        var valueDate = DateOnly.FromDateTime(DateTime.Today);
        var response = await api.AddTradeLiveFeedAsync(orderId, tradeId, valueDate);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task HaltTradeLiveFeed_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var orderId = 1;
        var tradeId = 1;
        var response = await api.HaltTradeLiveFeedAsync(orderId, tradeId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RemoveTradeLiveFeed_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var orderId = 1;
        var tradeId = 1;
        var valueDate = DateOnly.FromDateTime(DateTime.Today);
        var response = await api.RemoveTradeLiveFeedAsync(orderId, tradeId, valueDate);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RemoveTradeLiveFeeds_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var orderId = 1;
        var response = await api.RemoveTradeLiveFeedsAsync(orderId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task InsertFuturesEodData_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var valueDate = DateOnly.FromDateTime(DateTime.Now);
        var tickData = new FuturesTickDataV2ReadModel("TEST1", valueDate, 1L, TimeOnly.MinValue, 1m, 1);
        var contract = new FuturesContractV2ReadModel("TEST1", "desc", "SYM", "SYM1", "FUT", "USD", "CME", "50", valueDate, true);
        var eodDataToday = new FuturesEodDataV2ReadModel(
            "TEST1", valueDate, "SYM", 1m, 1m, 1m, 1m, 1, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
            TomasAI.IFM.Shared.MarketData.MarketDirectionType.NeutralDown,
            TomasAI.IFM.Shared.MarketData.MarketVolatilityType.Normal,
            TomasAI.IFM.Shared.MarketData.PriceDirectionType.Flat,
            TomasAI.IFM.Shared.MarketData.PriceVolatilityType.Flat,
            1.0, 1);
        var eodDataRange = new List<FuturesEodDataV2ReadModel> { eodDataToday };
        var normCurveData = new NormalCurveTableReadModel(new NormalCurveDataReadModel[0]);
        var windowSize = 1;
        var vixEodData = new List<VixFuturesEodDataReadModel> { new VixFuturesEodDataReadModel("TEST1", valueDate, 1, 1, 1, 1, 1) };
        var response = await api.InsertFuturesEodDataAsync(valueDate, tickData, contract, eodDataToday, eodDataRange, normCurveData, windowSize, vixEodData);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task InsertVixFuturesEodData_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var tickData = new FuturesTickDataV2ReadModel("TEST1", DateOnly.FromDateTime(DateTime.Now), 1L, TimeOnly.MinValue, 1m, 1);
        var response = await api.InsertVixFuturesEodDataAsync(tickData);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task InsertFuturesOptionQuoteData_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var quoteId = 1;
        var contractId = "TEST1";
        var quoteData = new QuoteData(DateTime.Now, TomasAI.IFM.Shared.MarketDataFeed.QuoteLevelType.LevelOne, 1, 1, TomasAI.IFM.Shared.MarketDataFeed.QuoteSide.Bid, TomasAI.IFM.Shared.MarketDataFeed.QuoteType.Price, 1.0, 1);
        var response = await api.InsertFuturesOptionQuoteDataAsync(quoteId, contractId, quoteData);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StartFuturesOptionQuoteDataStreaming_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var quoteId = 1;
        var optionQuotes = new[] { new FuturesOptionQuoteReadModel(1, "SYM20251010C3456", 1, "user", DateTime.Now) };
        var optionContracts = new[] { new FuturesOptionContractReadModel("SYM20251010C3456", "SYM", "SYM1", "FUT", "USD", "CME", "50", "OPT", DateOnly.FromDateTime(DateTime.Now), 1.0, "CALL") };
        var response = await api.StartFuturesOptionQuoteDataStreamingAsync(quoteId, optionQuotes, optionContracts);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StopFuturesOptionQuoteDataStreaming_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataFeedCommandApi(commandServiceApi);
        var quoteId = 1;
        var response = await api.StopFuturesOptionQuoteDataStreamingAsync(quoteId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    // Add similar [Fact] tests for each method in MarketDataFeedCommandApi following the above pattern.
}
