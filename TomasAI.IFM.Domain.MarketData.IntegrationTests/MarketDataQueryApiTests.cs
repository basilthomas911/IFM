using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

public class MarketDataQueryApiTests(TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint> factory, MarketDataFixture dbFixture)
    : IClassFixture<TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>>, IClassFixture<MarketDataFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetLastRateOfReturnQuery_Ok()
    {
        // arrange...
        var rateOfReturn = SampleData.RateOfReturn;
        await dbFixture.MarketDataDb.DeleteRateOfReturnAsync(rateOfReturn.Symbol, rateOfReturn.ValueDate);
        await dbFixture.MarketDataDb.InsertRateOfReturnAsync(rateOfReturn);

        // act...
        var marketDataApi = new MarketDataQueryApi(_actorProducer);
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
        var marketDataApi = new MarketDataQueryApi(_actorProducer);
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
        var marketDataApi = new MarketDataQueryApi(_actorProducer);
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
        var now = DateTimeOffset.Now;

        // act...
        var marketDataApi = new MarketDataQueryApi(_actorProducer);
        var response = await marketDataApi.GetValueDateAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        if (!FuturesTradingValueDate.TryGet(now, out var expectedValueDate))
        {
            response.Value.Should().BeNull();
        }
        else
        {
            response.Value.Should().NotBeNull();
            response.Value.Value.Should().Be(expectedValueDate);
            response.Value.Value.DayOfWeek.Should().NotBe(DayOfWeek.Saturday);
            response.Value.Value.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);
        }
    }

    [Fact]
    public async Task GetMarketSessionQuery_AlwaysReturnsOperationalDateAndExplicitLiveState()
    {
        var marketDataApi = new MarketDataQueryApi(_actorProducer);

        var response = await marketDataApi.GetMarketSessionAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.IsValid.Should().BeTrue();
        response.Value.ActiveValueDate.HasValue.Should().Be(response.Value.IsMarketOpen);
        response.Value.SessionEndUtc.Should().BeAfter(response.Value.SessionStartUtc);
        response.Value.NextTransitionUtc.Should().BeAfter(DateTime.UtcNow);
    }
}
