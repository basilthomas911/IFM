using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

public class ReferenceQueryApiTests(WebApplicationFactory<Program> factory, ReferenceFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<ReferenceFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task GetDefaultFuturesContractDefinitionsQuery_Ok()
    {
        // arrange...
        var currency = new LookupTypeReadModel(
            lookupTypeName: "DefaultFuturesContractCurrency",
            shortCode: "USD",
            orderId: 0,
            description: "Default Currency",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest");

        var exchange = new LookupTypeReadModel(
            lookupTypeName: "DefaultFuturesContractExchange",
            shortCode: "CME",
            orderId: 0,
            description: "Default Exchange",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest");

        var multiplier = new LookupTypeReadModel(
            lookupTypeName: "DefaultFuturesContractMultiplier",
            shortCode: "50",
            orderId: 0,
            description: "Default Multiplier",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest");

        var securityType = new LookupTypeReadModel(
            lookupTypeName: "DefaultFuturesContractSecurityType",
            shortCode: "FUT",
            orderId: 0,
            description: "Default Security Type",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest");

        var optionSecurityType = new LookupTypeReadModel(
            lookupTypeName: "DefaultFuturesOptionContractSecurityType",
            shortCode: "FOP",
            orderId: 0,
            description: "Default Option Security Type",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest");

        var symbol = new LookupTypeReadModel(
            lookupTypeName: "DefaultFuturesContractSymbol",
            shortCode: "ES",
            orderId: 0,
            description: "Default Symbol",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest");

        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(currency.Id);
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(exchange.Id);
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(multiplier.Id);
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(securityType.Id);
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(optionSecurityType.Id);
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(symbol.Id);

        await dbFixture.ReferenceDb.InsertLookupTypeAsync(currency);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(exchange);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(multiplier);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(securityType);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(optionSecurityType);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(symbol);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var referenceApi = new ReferenceQueryApi(queryServiceApi);
        var response = await referenceApi.GetDefaultFuturesContractDefinitionsAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Currency.Should().Be(currency.ShortCode);
        response.Value.Exchange.Should().Be(exchange.ShortCode);
        response.Value.Multiplier.Should().Be(multiplier.ShortCode);
        response.Value.SecurityType.Should().Be(securityType.ShortCode);
        response.Value.OptionSecurityType.Should().Be(optionSecurityType.ShortCode);
        response.Value.Symbol.Should().Be(symbol.ShortCode);
    }

    [Fact]
    public async Task GetNextSeedIdQuery_Ok()
    {
        // arrange...
        var seedType = "IntegrationTestSeedId";

         // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var referenceApi = new ReferenceQueryApi(queryServiceApi);
        var response = await referenceApi.GetNextSeedIdAsync(seedType);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Value.Should().NotBe(0);
    }

    [Fact]
    public async Task GetCurrentSeedIdQuery_Ok()
    {
        // arrange...
        var seedType = "IntegrationTestCurrentSeedId";

        // act - get next seed id first...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var referenceApi = new ReferenceQueryApi(queryServiceApi);
        var nextSeedIdResponse = await referenceApi.GetNextSeedIdAsync(seedType);

        // assert - verify next seed id is not zero...
        nextSeedIdResponse.Should().NotBeNull();
        nextSeedIdResponse.Success.Should().BeTrue();
        nextSeedIdResponse.Value.Should().NotBeNull();
        nextSeedIdResponse.Value.Value.Should().NotBe(0);

        // act - get current seed id...
        var currentSeedIdResponse = await referenceApi.GetCurrentSeedIdAsync(seedType);

        // assert - verify current seed id matches the previously generated seed id...
        currentSeedIdResponse.Should().NotBeNull();
        currentSeedIdResponse.Success.Should().BeTrue();
        currentSeedIdResponse.Value.Should().NotBeNull();
        currentSeedIdResponse.Value.Value.Should().Be(nextSeedIdResponse.Value.Value);
    }

    [Fact]
    public async Task GetFuturesOptionStrikePriceDefinitionsQuery_Ok()
    {
        // arrange...
        var minStrikePrice = new LookupTypeReadModel(
            lookupTypeName: "FuturesOptionStrikePriceMin",
            shortCode: "100",
            orderId: 0,
            description: "Minimum Strike Price",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest");

        var maxStrikePrice = new LookupTypeReadModel(
            lookupTypeName: "FuturesOptionStrikePriceMax",
            shortCode: "500",
            orderId: 0,
            description: "Maximum Strike Price",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest");

        var incrementStrikePrice = new LookupTypeReadModel(
            lookupTypeName: "FuturesOptionStrikePriceIncrement",
            shortCode: "5",
            orderId: 0,
            description: "Strike Price Increment",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest");

        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(minStrikePrice.Id);
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(maxStrikePrice.Id);
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(incrementStrikePrice.Id);

        await dbFixture.ReferenceDb.InsertLookupTypeAsync(minStrikePrice);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(maxStrikePrice);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(incrementStrikePrice);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var referenceApi = new ReferenceQueryApi(queryServiceApi);
        var response = await referenceApi.GetFuturesOptionStrikePriceDefinitionsAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Minimum.Should().Be(int.Parse(minStrikePrice.ShortCode));
        response.Value.Maximum.Should().Be(int.Parse(maxStrikePrice.ShortCode));
        response.Value.Increment.Should().Be(int.Parse(incrementStrikePrice.ShortCode));
    }

    [Fact]
    public async Task GetMDIForwardLossRatiosQuery_Ok()
    {
        // arrange...
        var trendDirection = IntrinsicTimeTrendType.UpTrend;
        var tradeType = TradeType.LongIronCondor;

        var mdiForwardLossRatio1 = new MDIForwardLossRatioReadModel(
            mdi: 10,
            trendDirection: trendDirection,
            tradeType: tradeType,
            forwardLossRatio: 0.15,
            createdBy: "IntegrationTest",
            createdOn: DateTime.UtcNow,
            updatedBy: "IntegrationTest",
            updatedOn: DateTime.UtcNow);

        var mdiForwardLossRatio2 = new MDIForwardLossRatioReadModel(
            mdi: 20,
            trendDirection: trendDirection,
            tradeType: tradeType,
            forwardLossRatio: 0.25,
            createdBy: "IntegrationTest",
            createdOn: DateTime.UtcNow,
            updatedBy: "IntegrationTest",
            updatedOn: DateTime.UtcNow);

        await dbFixture.ReferenceDb.DeleteMDIForwardLossRatioAsync(trendDirection, tradeType);

        await dbFixture.ReferenceDb.InsertMDIForwardLossRatioAsync(mdiForwardLossRatio1);
        await dbFixture.ReferenceDb.InsertMDIForwardLossRatioAsync(mdiForwardLossRatio2);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var referenceApi = new ReferenceQueryApi(queryServiceApi);
        var response = await referenceApi.GetMDIForwardLossRatiosAsync(trendDirection, tradeType);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().HaveCountGreaterThanOrEqualTo(2);
        response.Value.Should().Contain(r => r.MDI == mdiForwardLossRatio1.MDI && r.ForwardLossRatio == mdiForwardLossRatio1.ForwardLossRatio);
        response.Value.Should().Contain(r => r.MDI == mdiForwardLossRatio2.MDI && r.ForwardLossRatio == mdiForwardLossRatio2.ForwardLossRatio);
    }
}
