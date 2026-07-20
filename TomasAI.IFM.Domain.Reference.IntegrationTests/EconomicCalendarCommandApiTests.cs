using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

public class EconomicCalendarCommandApiTests(WebApplicationFactory<Program> factory, ReferenceFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<ReferenceFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

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
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.ReferenceDb.DeleteEconomicCalendarAsync(economicCalendar.Id);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var referenceApi = new ReferenceCommandApi(commandServiceApi);
        var response = await referenceApi.AddEconomicCalendarAsync(economicCalendar);

        // wait for denormalization to complete
        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // verify economic calendar was added to database
        var savedCalendar = await dbFixture.ReferenceDb.GetEconomicCalendarAsync(economicCalendar.Id);
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
        var subject = new ActorSubject(ActorType.Command, AddEconomicCalendarCommand.Actor, AddEconomicCalendarCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // ensure record exists first by adding it
        await dbFixture.ReferenceDb.DeleteEconomicCalendarAsync(economicCalendarId);
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var referenceApi = new ReferenceCommandApi(commandServiceApi);
        var response = await referenceApi.AddEconomicCalendarAsync(economicCalendar);
        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // verify economic calendar was added to database
        var addedCalendar = await dbFixture.ReferenceDb.GetEconomicCalendarAsync(economicCalendar.Id);
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
        commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        referenceApi = new ReferenceCommandApi(commandServiceApi);
        response = await referenceApi.ChangeEconomicCalendarAsync(economicCalendarId, updatedEconomicCalendar, overwrite: true);

        // wait for denormalization to complete
        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // verify economic calendar was changed in database
        var savedCalendar = await dbFixture.ReferenceDb.GetEconomicCalendarAsync(economicCalendarId);
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
        var subject = new ActorSubject(ActorType.Command, AddEconomicCalendarCommand.Actor, AddEconomicCalendarCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);


        // ensure clean state - delete if exists
        await dbFixture.ReferenceDb.DeleteEconomicCalendarAsync(economicCalendarId);

        // add economic calendar first
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var referenceApi = new ReferenceCommandApi(commandServiceApi);
        var addResponse = await referenceApi.AddEconomicCalendarAsync(economicCalendar);

        // wait for denormalization to complete
        await Task.Delay(1000);

        // verify economic calendar was added to database
        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue();
        addResponse.Value.Should().NotBe(Guid.Empty);

        var addedCalendar = await dbFixture.ReferenceDb.GetEconomicCalendarAsync(economicCalendarId);
        addedCalendar.Should().NotBeNull();
        addedCalendar!.CountryCode.Should().Be(economicCalendar.CountryCode);
        addedCalendar.EventName.Should().Be(economicCalendar.EventName);

        // act - remove economic calendar
        commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        referenceApi = new ReferenceCommandApi(commandServiceApi);
        var removeResponse = await referenceApi.RemoveEconomicCalendarAsync(economicCalendarId, overwrite: true);

        // wait for denormalization to complete
        await Task.Delay(1000);

        // assert...
        removeResponse.Should().NotBeNull();
        removeResponse.Success.Should().BeTrue();
        removeResponse.Value.Should().NotBe(Guid.Empty);

        // verify economic calendar was removed from database
        var removedCalendar = await dbFixture.ReferenceDb.GetEconomicCalendarAsync(economicCalendarId);
        removedCalendar.Should().BeNull();
    }

    [Fact]
    public async Task ImportEconomicCalendars_Ok()
    {
        // arrange...
        var importedDate = DateTime.UtcNow;
        var economicCalendars = SampleData.EconomicCalendars;

        // clean up any existing records
        foreach (var calendar in economicCalendars)
        {
            await dbFixture.ReferenceDb.DeleteEconomicCalendarAsync(calendar.Id);
        }

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var referenceApi = new ReferenceCommandApi(commandServiceApi);
        var response = await referenceApi.ImportEconomicCalendarsAsync(importedDate, economicCalendars);

        // wait for denormalization to complete
        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // verify all 5 economic calendars were added to database
        foreach (var calendar in economicCalendars)
        {
            var savedCalendar = await dbFixture.ReferenceDb.GetEconomicCalendarAsync(calendar.Id);
            savedCalendar.Should().NotBeNull();
            savedCalendar!.CountryCode.Should().Be(calendar.CountryCode);
            savedCalendar.EventName.Should().Be(calendar.EventName);
            savedCalendar.Actual.Should().Be(calendar.Actual);
            savedCalendar.Forecast.Should().Be(calendar.Forecast);
            savedCalendar.Prior.Should().Be(calendar.Prior);
        }
    }
}