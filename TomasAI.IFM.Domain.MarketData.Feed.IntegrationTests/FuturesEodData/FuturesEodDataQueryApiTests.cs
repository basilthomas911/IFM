using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesEodData;

public class FuturesEodDataQueryApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetFuturesEodDataByDateRange_Ok()
    {
        // arrange...
        var contractId = SampleData.FuturesContractId;
        var eodDataRange = SampleData.FuturesEodDataRange;
        var startDate = eodDataRange.Last().ValueDate;
        var endDate = eodDataRange.First().ValueDate;

        foreach (var eodData in eodDataRange)
            await dbFixture.MarketDataDb.DeleteFuturesEodDataAsync(eodData.ContractId, eodData.ValueDate);
        await dbFixture.MarketDataDb.InsertFuturesEodDataAsync(eodDataRange);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetFuturesEodDataAsync(contractId, startDate, endDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNullOrEmpty();
        response.Value!.Length.Should().Be(eodDataRange.Count);

        foreach (var expected in eodDataRange)
        {
            var actual = response.Value.FirstOrDefault(e => e.ValueDate == expected.ValueDate);
            actual.Should().NotBeNull();
            actual!.ContractId.Should().Be(expected.ContractId);
            actual.Symbol.Should().Be(expected.Symbol);
            actual.OpenPrice.Should().Be(expected.OpenPrice);
            actual.HighPrice.Should().Be(expected.HighPrice);
            actual.LowPrice.Should().Be(expected.LowPrice);
            actual.ClosePrice.Should().Be(expected.ClosePrice);
            actual.Volume.Should().Be(expected.Volume);
        }
    }

    [Fact]
    public async Task GetFuturesEodDataParameters_Ok()
    {
        // arrange...
        var contractId = SampleData.FuturesContractId;
        var eodDataToday = SampleData.FuturesEodData;
        var valueDate = eodDataToday.ValueDate;
        var eodDataRange = SampleData.FuturesEodDataRange
            .Where(e => e.ValueDate < valueDate && e.ValueDate >= valueDate.AddMonths(-2))
            .ToList();
        var futuresDataId = new FuturesDataId(contractId, valueDate);
        var lastFuturesEodData = await dbFixture.MarketDataDb.GetLastFuturesEodDataAsync(contractId, valueDate);
        await dbFixture.MarketDataDb.DeleteFuturesClosingPriceAsync(lastFuturesEodData?.ContractId!, lastFuturesEodData?.ValueDate ?? default);
        await dbFixture.MarketDataDb.InsertFuturesClosingPriceAsync(new FuturesClosingPriceReadModel
        {
            ContractId = lastFuturesEodData?.ContractId!,
            ValueDate = lastFuturesEodData?.ValueDate ?? default,
            ClosingPrice = lastFuturesEodData?.ClosePrice ?? default,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "IntegrationTest"
        });
        var yesterDaysClosingPrice = lastFuturesEodData?.ClosePrice ?? default;

        await dbFixture.MarketDataDb.DeleteFuturesEodDataAsync(contractId, valueDate);
        foreach (var eodData in eodDataRange)
            await dbFixture.MarketDataDb.DeleteFuturesEodDataAsync(eodData.ContractId, eodData.ValueDate);
        await dbFixture.MarketDataDb.InsertFuturesEodDataAsync(eodDataToday);
        await dbFixture.MarketDataDb.InsertFuturesEodDataAsync(eodDataRange);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetFuturesEodDataParametersAsync(contractId, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();

        // verify today's EOD data was returned from database
        response.Value!.FuturesEodDataToday.Should().NotBeNull();
        response.Value.FuturesEodDataToday.ContractId.Should().Be(eodDataToday.ContractId);
        response.Value.FuturesEodDataToday.ValueDate.Should().Be(eodDataToday.ValueDate);
        response.Value.FuturesEodDataToday.Symbol.Should().Be(eodDataToday.Symbol);
        response.Value.FuturesEodDataToday.OpenPrice.Should().Be(yesterDaysClosingPrice);
        response.Value.FuturesEodDataToday.HighPrice.Should().Be(eodDataToday.HighPrice);
        response.Value.FuturesEodDataToday.LowPrice.Should().Be(eodDataToday.LowPrice);
        response.Value.FuturesEodDataToday.ClosePrice.Should().Be(eodDataToday.ClosePrice);
        response.Value.FuturesEodDataToday.Volume.Should().Be(eodDataToday.Volume);

        // verify EOD data range was returned from database
        response.Value.FuturesEodDataRange.Should().NotBeNullOrEmpty();
        response.Value.FuturesEodDataRange.Length.Should().BeGreaterThanOrEqualTo(eodDataRange.Count);

        foreach (var expected in eodDataRange)
        {
            var actual = response.Value.FuturesEodDataRange.FirstOrDefault(e => e.ValueDate == expected.ValueDate);
            actual.Should().NotBeNull();
            actual!.ContractId.Should().Be(expected.ContractId);
            actual.Symbol.Should().Be(expected.Symbol);
            actual.ClosePrice.Should().Be(expected.ClosePrice);
        }

        // verify normal curve table was returned from database
        //response.Value.NormalCurveTable.Should().NotBeNull();
        //response.Value.NormalCurveTable.NormalCurveTable.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetFuturesEodData_Ok()
    {
        // arrange...
        var contractId = SampleData.FuturesContractId;
        var eodData = SampleData.FuturesEodData;
        var valueDate = eodData.ValueDate;

        var futuresDataId = new FuturesDataId(contractId, valueDate);
        var yesterdaysClosingPrice = (await dbFixture.MarketDataDb.GetYesterdaysFuturesClosingPriceAsync(futuresDataId))?.ClosingPrice;

        await dbFixture.MarketDataDb.DeleteFuturesEodDataAsync(contractId, valueDate);
        await dbFixture.MarketDataDb.InsertFuturesEodDataAsync(eodData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetFuturesEodDataAsync(contractId, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(eodData.ContractId);
        response.Value.ValueDate.Should().Be(eodData.ValueDate);
        response.Value.Symbol.Should().Be(eodData.Symbol);
        response.Value.OpenPrice.Should().Be(yesterdaysClosingPrice);
        response.Value.HighPrice.Should().Be(eodData.HighPrice);
        response.Value.LowPrice.Should().Be(eodData.LowPrice);
        response.Value.ClosePrice.Should().Be(eodData.ClosePrice);
        response.Value.Volume.Should().Be(eodData.Volume);
    }

    [Fact]
    public async Task GetLastFuturesEodData_Ok()
    {
        // arrange...
        var contractId = SampleData.FuturesContractId;
        var eodData = SampleData.FuturesEodData;
        var valueDate = eodData.ValueDate;

        var lastFuturesEodData = await dbFixture.MarketDataDb.GetLastFuturesEodDataAsync(contractId, valueDate);
        Assert.NotNull(lastFuturesEodData);
        await dbFixture.MarketDataDb.DeleteFuturesEodDataAsync(contractId, valueDate);
        await dbFixture.MarketDataDb.InsertFuturesEodDataAsync(eodData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetLastFuturesEodDataAsync(contractId, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(lastFuturesEodData.ContractId);
        response.Value.ValueDate.Should().Be(lastFuturesEodData.ValueDate);
        response.Value.Symbol.Should().Be(lastFuturesEodData.Symbol);
        response.Value.HighPrice.Should().Be(lastFuturesEodData.HighPrice);
        response.Value.LowPrice.Should().Be(lastFuturesEodData.LowPrice);
        response.Value.ClosePrice.Should().Be(lastFuturesEodData.ClosePrice);
        response.Value.Volume.Should().Be(lastFuturesEodData.Volume);
    }

    [Fact]
    public async Task GetFuturesEodMovingAverages_Ok()
    {
        // arrange...
        var contractId = SampleData.FuturesContractId;
        var symbol = SampleData.Symbol;
        var valueDate = SampleData.ValueDate;
        var eodDataRange = SampleData.FuturesEodDataRange;

        foreach (var eodData in eodDataRange)
            await dbFixture.MarketDataDb.DeleteFuturesEodDataAsync(eodData.ContractId, eodData.ValueDate);
        await dbFixture.MarketDataDb.InsertFuturesEodDataAsync(eodDataRange);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetFuturesEodMovingAveragesAsync(contractId, symbol, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.Symbol.Should().Be(symbol);
        response.Value.ValueDate.Should().Be(valueDate);
        response.Value.FiftyDMA.Should().BeGreaterThan(0);
        response.Value.TwoHundredDMA.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetLastVixFuturesEodData_Ok()
    {
        // arrange...
        var valueDate = SampleData.ValueDate;
        var vixContractId = "VX20251219";
        var vixFuturesTickData = new FuturesTickDataV2ReadModel(
            contractId: vixContractId,
            valueDate: valueDate,
            tickId: 1,
            tickTime: TimeOnly.FromDateTime(DateTime.UtcNow),
            price: 18.50m,
            size: 100);

        await dbFixture.MarketDataDb.DeleteVixFuturesEodDataAsync(vixContractId, valueDate);
        await dbFixture.MarketDataDb.InsertVixFuturesEodDataAsync(vixFuturesTickData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetLastVixFuturesEodDataAsync(vixContractId, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(vixContractId);
        response.Value.ValueDate.Should().Be(valueDate);
    }

    [Fact]
    public async Task GetVixFuturesEodData_Ok()
    {
        // arrange...
        var valueDate = SampleData.ValueDate;
        var vixContractId = "VX20251219";
        var vixFuturesTickData = new FuturesTickDataV2ReadModel(
            contractId: vixContractId,
            valueDate: valueDate,
            tickId: 1,
            tickTime: TimeOnly.FromDateTime(DateTime.UtcNow),
            price: 18.50m,
            size: 100);

        await dbFixture.MarketDataDb.DeleteVixFuturesEodDataAsync(vixContractId, valueDate);
        await dbFixture.MarketDataDb.InsertVixFuturesEodDataAsync(vixFuturesTickData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetVixFuturesEodDataAsync(vixContractId, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNullOrEmpty();

        var actual = response.Value!.FirstOrDefault(e => e.ValueDate == valueDate);
        actual.Should().NotBeNull();
        actual!.ContractId.Should().Be(vixContractId);
        actual.ValueDate.Should().Be(valueDate);
    }
}
