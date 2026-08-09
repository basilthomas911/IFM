using TomasAI.IFM.Domain.Reference.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

public class EconomicCalendarQueryApiTests(WebApplicationFactory<Program> factory, ReferenceFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<ReferenceFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetEconomicCalendarAllQuery_Ok()
    {
        // arrange...
        var economicCalendar = SampleData.EconomicCalendar1;
        await dbFixture.ReferenceDb.DeleteEconomicCalendarAsync(economicCalendar.Id);
        await dbFixture.ReferenceDb.InsertEconomicCalendarAsync(economicCalendar);

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetEconomicCalendarsAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(e =>
            e.EventDate == economicCalendar.EventDate &&
            e.CountryCode == economicCalendar.CountryCode &&
            e.EventName == economicCalendar.EventName);
    }

    [Fact]
    public async Task GetEconomicCalendarQuery_Ok()
    {
        // arrange...
        var todaysDate = DateTime.UtcNow;
        var economicCalendar = SampleData.EconomicCalendar1 with
        {
            EventDate = todaysDate.AddHours(14).AddMinutes(30)
        };
        await dbFixture.ReferenceDb.DeleteEconomicCalendarAsync(economicCalendar.Id);
        await dbFixture.ReferenceDb.InsertEconomicCalendarAsync(economicCalendar);

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetEconomicCalendarAsync(todaysDate,EconomicCalendarViewType.Today, economicCalendar.CountryCode);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().Contain(e =>
            e.CountryCode == economicCalendar.CountryCode &&
            e.EventName == economicCalendar.EventName);
    }

    [Fact]
    public async Task GetEconomicCalendarQuery_Tomorrow_Ok()
    {
        // arrange...
        var todaysDate = DateTime.UtcNow.Date;
        var economicCalendar = SampleData.EconomicCalendar2 with
        {
            EventDate = todaysDate.AddDays(1).AddHours(10)
        };
        await dbFixture.ReferenceDb.DeleteEconomicCalendarAsync(economicCalendar.Id);
        await dbFixture.ReferenceDb.InsertEconomicCalendarAsync(economicCalendar);

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetEconomicCalendarAsync(todaysDate, EconomicCalendarViewType.Tomorrow, economicCalendar.CountryCode);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().Contain(e =>
            e.CountryCode == economicCalendar.CountryCode &&
            e.EventName == economicCalendar.EventName);
    }

    [Fact]
    public async Task GetEconomicCalendarQuery_Yesterday_Ok()
    {
        // arrange...
        var todaysDate = DateTime.UtcNow.Date;
        var economicCalendar = SampleData.EconomicCalendar3 with
        {
            EventDate = todaysDate.AddDays(-1).AddHours(9)
        };
        await dbFixture.ReferenceDb.DeleteEconomicCalendarAsync(economicCalendar.Id);
        await dbFixture.ReferenceDb.InsertEconomicCalendarAsync(economicCalendar);

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetEconomicCalendarAsync(todaysDate, EconomicCalendarViewType.Yesterday, economicCalendar.CountryCode);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().Contain(e =>
            e.CountryCode == economicCalendar.CountryCode &&
            e.EventName == economicCalendar.EventName);
    }

    [Fact]
    public async Task GetEconomicCalendarDateQuery_Today_Ok()
    {
        // arrange...
        var todaysDate = DateTime.UtcNow;

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetEconomicCalendarDateAsync(todaysDate, EconomicCalendarViewType.Today);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetEconomicCalendarDateQuery_Tomorrow_Ok()
    {
        // arrange...
        var todaysDate = DateTime.UtcNow;

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetEconomicCalendarDateAsync(todaysDate, EconomicCalendarViewType.Tomorrow);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetEconomicCalendarDateQuery_Yesterday_Ok()
    {
        // arrange...
        var todaysDate = DateTime.UtcNow;

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetEconomicCalendarDateAsync(todaysDate, EconomicCalendarViewType.Yesterday);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetEconomicCalendarDateQuery_ThisWeek_Ok()
    {
        // arrange...
        var todaysDate = DateTime.UtcNow;

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetEconomicCalendarDateAsync(todaysDate, EconomicCalendarViewType.ThisWeek);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetEconomicCalendarDateQuery_NextWeek_Ok()
    {
        // arrange...
        var todaysDate = DateTime.UtcNow;

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetEconomicCalendarDateAsync(todaysDate, EconomicCalendarViewType.NextWeek);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetEconomicCalendarCountryCodesQuery_Ok()
    {
        // arrange...
        var economicCalendar = SampleData.EconomicCalendar1;
        await dbFixture.ReferenceDb.DeleteEconomicCalendarAsync(economicCalendar.Id);
        await dbFixture.ReferenceDb.InsertEconomicCalendarAsync(economicCalendar);

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetEconomicCalendarCountryCodesAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(e => e.CountryCode == economicCalendar.CountryCode);
    }

    [Fact]
    public async Task GetExternalEconomicCalendarsQuery_Ok()
    {
        // arrange...
        // Note: This test assumes external economic calendar source is available

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetExternalEconomicCalendarsAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetEconomicCalendarAllQuery_MultipleEntries_Ok()
    {
        // arrange...
        var economicCalendars = SampleData.EconomicCalendars;
        foreach (var calendar in economicCalendars)
        {
            await dbFixture.ReferenceDb.DeleteEconomicCalendarAsync(calendar.Id);
            await dbFixture.ReferenceDb.InsertEconomicCalendarAsync(calendar);
        }

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetEconomicCalendarsAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Length.Should().BeGreaterThanOrEqualTo(economicCalendars.Length);
    }

    [Fact]
    public async Task GetEconomicCalendarQuery_FilterByCountry_Ok()
    {
        // arrange...
        var todaysDate = DateTime.UtcNow.Date;
        var usCalendar = SampleData.EconomicCalendar1 with { EventDate = todaysDate.AddHours(14) };
        var euCalendar = SampleData.EconomicCalendar3 with { EventDate = todaysDate.AddHours(9) };

        await dbFixture.ReferenceDb.DeleteEconomicCalendarAsync(usCalendar.Id);
        await dbFixture.ReferenceDb.DeleteEconomicCalendarAsync(euCalendar.Id);
        await dbFixture.ReferenceDb.InsertEconomicCalendarAsync(usCalendar);
        await dbFixture.ReferenceDb.InsertEconomicCalendarAsync(euCalendar);

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var usResponse = await referenceApi.GetEconomicCalendarAsync(todaysDate, EconomicCalendarViewType.Today, "US");
        var euResponse = await referenceApi.GetEconomicCalendarAsync(todaysDate, EconomicCalendarViewType.Today, "EU");

        // assert...
        usResponse.Should().NotBeNull();
        usResponse.Success.Should().BeTrue();
        usResponse.Value.Should().NotBeNull();
        usResponse.Value.Should().OnlyContain(e => e.CountryCode == "US");

        euResponse.Should().NotBeNull();
        euResponse.Success.Should().BeTrue();
        euResponse.Value.Should().NotBeNull();
        euResponse.Value.Should().OnlyContain(e => e.CountryCode == "EU");
    }
}
