using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

public class MarketDataQueryApiTests(WebApplicationFactory<Program> factory, MarketDataFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetLastRateOfReturnQuery_Ok()
    {
        // arrange...
        var rateOfReturn = SampleData.RateOfReturn;
        await dbFixture.MarketDataDb.DeleteRateOfReturnAsync(rateOfReturn.Symbol, rateOfReturn.ValueDate);
        await dbFixture.MarketDataDb.InsertRateOfReturnAsync(rateOfReturn);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataQueryApi(queryServiceApi);
        var response = await marketDataApi.GetLastRateOfReturnAsync(rateOfReturn.Symbol, rateOfReturn.ValueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Symbol.Should().Be(rateOfReturn.Symbol);
        response.Value.ValueDate.Should().Be(rateOfReturn.ValueDate);
        response.Value.RateOfReturn.Should().Be(rateOfReturn.RateOfReturn);
    }

    [Fact]
    public async Task GetTradingDaysQuery_Ok()
    {
        // arrange...
        var holiday = SampleData.MarketHoliday;
        await dbFixture.MarketDataDb.DeleteMarketHolidayAsync(holiday);
        await dbFixture.MarketDataDb.InsertMarketHolidayAsync(holiday);

        // query a week that contains the holiday (Mon Jun 30 � Fri Jul 4, 2025)
        var startDate = new DateOnly(2025, 6, 30);
        var endDate = new DateOnly(2025, 7, 4);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataQueryApi(queryServiceApi);
        var response = await marketDataApi.GetTradingDaysAsync(startDate, endDate, MarketType.Futures, CurrencyType.USD);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Value.Should().BeGreaterThan(0);
        response.Value.Value.Should().Be(4); // 5 weekdays minus 1 holiday (Jul 4)
    }

    [Fact]
    public async Task GetTradingDatesQuery_Ok()
    {
        // arrange...
        var holiday = SampleData.MarketHoliday;
        await dbFixture.MarketDataDb.DeleteMarketHolidayAsync(holiday);
        await dbFixture.MarketDataDb.InsertMarketHolidayAsync(holiday);

        // query a week that contains the holiday (Mon Jun 30 � Fri Jul 4, 2025)
        var startDate = new DateOnly(2025, 6, 30);
        var endDate = new DateOnly(2025, 7, 4);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataQueryApi(queryServiceApi);
        var response = await marketDataApi.GetTradingDatesAsync(startDate, endDate, MarketType.Futures, CurrencyType.USD);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().HaveCount(4); // 5 weekdays minus 1 holiday (Jul 4)
        response.Value.Should().NotContain(holiday.HolidayDate);
        response.Value.Should().Contain(startDate); // Mon Jun 30
    }

    [Fact]
    public async Task GetValueDateQuery_Ok()
    {
        // arrange...
        var today = DateTime.Now;

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataQueryApi(queryServiceApi);
        var response = await marketDataApi.GetValueDateAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        if (today.DayOfWeek == DayOfWeek.Saturday)
        {
            // The futures market is closed on Saturday, so there is no active value date.
            response.Value.Should().BeNull();
        }
        else if (today.DayOfWeek == DayOfWeek.Sunday && today.TimeOfDay < TimeSpan.FromHours(18))
        {
            // The futures market remains closed until Sunday at 6 PM.
            response.Value.Should().BeNull();
        }
        else
        {
            response.Value.Should().NotBeNull();
            response.Value.Value.Should().BeOnOrAfter(DateOnly.FromDateTime(today));
            response.Value.Value.DayOfWeek.Should().NotBe(DayOfWeek.Saturday);
            response.Value.Value.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);
        }
    }

    [Fact]
    public async Task GetMarketSessionQuery_AlwaysReturnsOperationalDateAndExplicitLiveState()
    {
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(
            _httpClientFactory,
            _jsonSerializer,
            new QueryServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataQueryApi(queryServiceApi);

        var response = await marketDataApi.GetMarketSessionAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.IsValid.Should().BeTrue();
        response.Value.ActiveValueDate.HasValue.Should().Be(response.Value.IsLiveSessionOpen);
        response.Value.SessionEndUtc.Should().BeAfter(response.Value.SessionStartUtc);
    }
}
