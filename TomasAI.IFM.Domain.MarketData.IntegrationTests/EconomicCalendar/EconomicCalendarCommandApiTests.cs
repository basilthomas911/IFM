using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

public class EconomicCalendarCommandApiTests(WebApplicationFactory<Program> factory, MarketDataFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFixture>
{
    static readonly TimeSpan StateTimeout = TimeSpan.FromSeconds(30);
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();

    [Fact]
    public async Task AddEconomicCalendar_Ok()
    {
        // arrange...
        var economicCalendar = new EconomicCalendarReadModel(
            eventDate: DateTime.Now.AddDays(1),
            countryCode: "US",
            eventName: "Test Economic Event",
            actual: "2.5%",
            forecast: "2.3%",
            prior: "2.1%",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest"
        );
        var entityId = new EconomicCalendarId(economicCalendar.EventDate, economicCalendar.CountryCode, economicCalendar.EventName);
        var subject = new ActorSubject(ActorType.Command, AddEconomicCalendarCommand.Actor, AddEconomicCalendarCommand.Verb, entityId.Format());
        await ClearEventStreamAsync(subject);
        await dbFixture.MarketDataDb.DeleteEconomicCalendarAsync(economicCalendar.Id);

        // act...
        var marketDataApi = new MarketDataCommandApi(_actorProducer);
        var response = await marketDataApi.AddEconomicCalendarAsync(economicCalendar);

        await WaitUntilAsync(async () =>
            await dbFixture.MarketDataDb.GetEconomicCalendarAsync(economicCalendar.Id) is not null);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);

        // verify economic calendar was added to database
        var savedCalendar = await dbFixture.MarketDataDb.GetEconomicCalendarAsync(economicCalendar.Id);
        savedCalendar.Should().NotBeNull();
        savedCalendar!.CountryCode.Should().Be(economicCalendar.CountryCode);
        savedCalendar.EventName.Should().Be(economicCalendar.EventName);
        savedCalendar.Actual.Should().Be(economicCalendar.Actual);
        savedCalendar.Forecast.Should().Be(economicCalendar.Forecast);
        savedCalendar.Prior.Should().Be(economicCalendar.Prior);
    }

    [Fact]
    public async Task ChangeEconomicCalendar_Ok()
    {
        // arrange...
        var eventDate = DateTime.Now.AddDays(2);
        var economicCalendarId = new EconomicCalendarId(eventDate, "US", "Test Change Event");
        var economicCalendar = new EconomicCalendarReadModel(
            eventDate: eventDate,
            countryCode: "US",
            eventName: "Test Change Event",
            actual: "3.0%",
            forecast: "2.8%",
            prior: "2.5%",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest"
        );

        var entityId = new EconomicCalendarId(economicCalendar.EventDate, economicCalendar.CountryCode, economicCalendar.EventName);
        await ClearEventStreamAsync(new ActorSubject(
            ActorType.Command, AddEconomicCalendarCommand.Actor, AddEconomicCalendarCommand.Verb, entityId.Format()));
        await ClearEventStreamAsync(new ActorSubject(
            ActorType.Command, ChangeEconomicCalendarCommand.Actor, ChangeEconomicCalendarCommand.Verb, entityId.Format()));

        // ensure record exists first by adding it
        await dbFixture.MarketDataDb.DeleteEconomicCalendarAsync(economicCalendarId);
        var marketDataApi = new MarketDataCommandApi(_actorProducer);
        var response = await marketDataApi.AddEconomicCalendarAsync(economicCalendar);
        await WaitUntilAsync(async () =>
            await dbFixture.MarketDataDb.GetEconomicCalendarAsync(economicCalendar.Id) is not null);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);

        // verify economic calendar was added to database
        var addedCalendar = await dbFixture.MarketDataDb.GetEconomicCalendarAsync(economicCalendar.Id);
        addedCalendar.Should().NotBeNull();
        addedCalendar!.CountryCode.Should().Be(economicCalendar.CountryCode);
        addedCalendar.EventName.Should().Be(economicCalendar.EventName);
        addedCalendar.Actual.Should().Be(economicCalendar.Actual);
        addedCalendar.Forecast.Should().Be(economicCalendar.Forecast);
        addedCalendar.Prior.Should().Be(economicCalendar.Prior);

        // update the economic calendar with new values
        var updatedEconomicCalendar = new EconomicCalendarReadModel(
            eventDate: eventDate,
            countryCode: "US",
            eventName: "Test Change Event",
            actual: "3.5%",
            forecast: "3.2%",
            prior: "3.0%",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest-Updated"
        );

        // act...
        response = await marketDataApi.ChangeEconomicCalendarAsync(economicCalendarId, updatedEconomicCalendar, overwrite: true);

        await WaitUntilAsync(async () =>
            (await dbFixture.MarketDataDb.GetEconomicCalendarAsync(economicCalendarId))?.Actual == updatedEconomicCalendar.Actual);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);

        // verify economic calendar was changed in database
        var savedCalendar = await dbFixture.MarketDataDb.GetEconomicCalendarAsync(economicCalendarId);
        savedCalendar.Should().NotBeNull();
        savedCalendar!.CountryCode.Should().Be(updatedEconomicCalendar.CountryCode);
        savedCalendar.EventName.Should().Be(updatedEconomicCalendar.EventName);
        savedCalendar.Actual.Should().Be(updatedEconomicCalendar.Actual);
        savedCalendar.Forecast.Should().Be(updatedEconomicCalendar.Forecast);
        savedCalendar.Prior.Should().Be(updatedEconomicCalendar.Prior);
    }

    [Fact]
    public async Task RemoveEconomicCalendar_Ok()
    {
        // arrange...
        var eventDate = DateTime.Now.AddDays(3);
        var economicCalendarId = new EconomicCalendarId(eventDate, "US", "Test Remove Event");
        var economicCalendar = new EconomicCalendarReadModel(
            eventDate: eventDate,
            countryCode: "US",
            eventName: "Test Remove Event",
            actual: "4.0%",
            forecast: "3.8%",
            prior: "3.5%",
            createdOn: DateTime.UtcNow,
            createdBy: "IntegrationTest"
        );
        var entityId = new EconomicCalendarId(economicCalendar.EventDate, economicCalendar.CountryCode, economicCalendar.EventName);
        await ClearEventStreamAsync(new ActorSubject(
            ActorType.Command, AddEconomicCalendarCommand.Actor, AddEconomicCalendarCommand.Verb, entityId.Format()));
        await ClearEventStreamAsync(new ActorSubject(
            ActorType.Command, RemoveEconomicCalendarCommand.Actor, RemoveEconomicCalendarCommand.Verb, entityId.Format()));


        // ensure clean state - delete if exists
        await dbFixture.MarketDataDb.DeleteEconomicCalendarAsync(economicCalendarId);

        // add economic calendar first
        var marketDataApi = new MarketDataCommandApi(_actorProducer);
        var addResponse = await marketDataApi.AddEconomicCalendarAsync(economicCalendar);

        await WaitUntilAsync(async () =>
            await dbFixture.MarketDataDb.GetEconomicCalendarAsync(economicCalendarId) is not null);

        // verify economic calendar was added to database
        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue(addResponse.ErrorMessage);
        addResponse.Value.Should().NotBe(Guid.Empty);

        var addedCalendar = await dbFixture.MarketDataDb.GetEconomicCalendarAsync(economicCalendarId);
        addedCalendar.Should().NotBeNull();
        addedCalendar!.CountryCode.Should().Be(economicCalendar.CountryCode);
        addedCalendar.EventName.Should().Be(economicCalendar.EventName);

        // act - remove economic calendar
        var removeResponse = await marketDataApi.RemoveEconomicCalendarAsync(economicCalendarId, overwrite: true);

        await WaitUntilAsync(async () =>
            await dbFixture.MarketDataDb.GetEconomicCalendarAsync(economicCalendarId) is null);

        // assert...
        removeResponse.Should().NotBeNull();
        removeResponse.Success.Should().BeTrue(removeResponse.ErrorMessage);
        removeResponse.Value.Should().NotBe(Guid.Empty);

        // verify economic calendar was removed from database
        var removedCalendar = await dbFixture.MarketDataDb.GetEconomicCalendarAsync(economicCalendarId);
        removedCalendar.Should().BeNull();
    }

    [Fact]
    public async Task ImportEconomicCalendars_Ok()
    {
        // arrange...
        var importedDate = DateTime.UtcNow;
        var runId = Guid.NewGuid().ToString("N");
        var economicCalendars = SampleData.EconomicCalendars
            .Select(calendar => calendar with { EventName = $"{calendar.EventName}-{runId}" })
            .ToArray();
        var importEntityId = new EconomicCalendarId(importedDate, "ZZ", "ImportEconomicCalendars");
        await ClearEventStreamAsync(new ActorSubject(
            ActorType.Command, ImportEconomicCalendarsCommand.Actor, ImportEconomicCalendarsCommand.Verb, importEntityId.Format()));

        // clean up any existing records
        foreach (var calendar in economicCalendars)
        {
            await dbFixture.MarketDataDb.DeleteEconomicCalendarAsync(calendar.Id);
        }

        // act...
        var marketDataApi = new MarketDataCommandApi(_actorProducer);
        var response = await marketDataApi.ImportEconomicCalendarsAsync(importedDate, economicCalendars);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);

        await WaitUntilAsync(async () =>
            (await dbFixture.MarketDataDb.GetEconomicCalendarAllAsync())
                .Count(calendar => calendar.EventName.EndsWith(runId, StringComparison.Ordinal))
            == economicCalendars.Length);

        // assert...
        // verify all 5 economic calendars were added to database
        foreach (var calendar in economicCalendars)
        {
            var savedCalendar = await dbFixture.MarketDataDb.GetEconomicCalendarAsync(calendar.Id);
            savedCalendar.Should().NotBeNull();
            savedCalendar!.CountryCode.Should().Be(calendar.CountryCode);
            savedCalendar.EventName.Should().Be(calendar.EventName);
            savedCalendar.Actual.Should().Be(calendar.Actual);
            savedCalendar.Forecast.Should().Be(calendar.Forecast);
            savedCalendar.Prior.Should().Be(calendar.Prior);
        }
    }

    async Task ClearEventStreamAsync(ActorSubject subject)
    {
        dbFixture.BlackboardService.EventSourcing.EventStreamId.Remove($"{subject.ThreadId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
    }

    static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(StateTimeout);
        while (!await condition())
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
    }
}
