using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference;
using System;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

/// <summary>
/// Integration tests for ReferenceCommandApi covering all IReferenceCommandApi methods.
/// </summary>
public class ReferenceCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Verify AddEconomicCalendarAsync returns success and a non-empty command id.
    /// </summary>
    [Fact]
    public async Task AddEconomicCalendar_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new ReferenceCommandApi(commandServiceApi);

        var economicCalendar = new EconomicCalendarReadModel(
            eventDate: DateTime.Now,
            countryCode: "USA",
            eventName: "Test Event",
            actual: "1",
            forecast: "2",
            prior: "0",
            createdOn: DateTime.Now,
            createdBy: "TestUser"
        );

        var response = await api.AddEconomicCalendarAsync(economicCalendar);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Verify RemoveEconomicCalendarAsync returns success and a non-empty command id.
    /// </summary>
    [Fact]
    public async Task RemoveEconomicCalendar_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new ReferenceCommandApi(commandServiceApi);

        var id = new EconomicCalendarId(DateTime.Now.Date, "USA", "Test Event");
        var response = await api.RemoveEconomicCalendarAsync(id, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Verify ChangeEconomicCalendarAsync returns success and a non-empty command id.
    /// </summary>
    [Fact]
    public async Task ChangeEconomicCalendar_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new ReferenceCommandApi(commandServiceApi);

        var id = new EconomicCalendarId(DateTime.Now.Date, "USA", "Test Event");
        var economicCalendar = new EconomicCalendarReadModel(
            eventDate: id.EventDate,
            countryCode: id.CountryCode,
            eventName: id.EventName,
            actual: "Updated",
            forecast: "UpdatedForecast",
            prior: "UpdatedPrior",
            createdOn: DateTime.Now,
            createdBy: "TestUser"
        );

        var response = await api.ChangeEconomicCalendarAsync(id, economicCalendar, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Verify ImportEconomicCalendarsAsync returns success and a non-empty command id.
    /// </summary>
    [Fact]
    public async Task ImportEconomicCalendars_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new ReferenceCommandApi(commandServiceApi);

        var economicCalendars = new[]
        {
            new EconomicCalendarReadModel(
                eventDate: DateTime.Now,
                countryCode: "USA",
                eventName: "Imported Event",
                actual: "0",
                forecast: "0",
                prior: "0",
                createdOn: DateTime.Now,
                createdBy: "TestUser"
            )
        };

        var response = await api.ImportEconomicCalendarsAsync(DateTime.Now.Date, economicCalendars);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Verify AddLookupTypeAsync returns success and a non-empty command id.
    /// </summary>
    [Fact]
    public async Task AddLookupType_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new ReferenceCommandApi(commandServiceApi);

        var lookupType = new LookupTypeReadModel(
            lookupTypeName: "TestType",
            shortCode: "TST",
            orderId: 0,
            description: "Test Description",
            createdOn: DateTime.Now,
            createdBy: "TestUser"
        );

        var response = await api.AddLookupTypeAsync(lookupType);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Verify RemoveLookupTypeAsync returns success and a non-empty command id.
    /// </summary>
    [Fact]
    public async Task RemoveLookupType_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new ReferenceCommandApi(commandServiceApi);

        var lookupTypeId = new LookupTypeId("TestType", 0);
        var response = await api.RemoveLookupTypeAsync(lookupTypeId, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Verify ChangeLookupTypeAsync returns success and a non-empty command id.
    /// </summary>
    [Fact]
    public async Task ChangeLookupType_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new ReferenceCommandApi(commandServiceApi);

        var lookupTypeId = new LookupTypeId("TestType", 0);
        var lookupType = new LookupTypeReadModel(
            lookupTypeName: lookupTypeId.LookupTypeName,
            shortCode: "TST",
            orderId: lookupTypeId.OrderId,
            description: "Updated Description",
            createdOn: DateTime.Now,
            createdBy: "TestUser"
        );

        var response = await api.ChangeLookupTypeAsync(lookupTypeId, lookupType, overwrite: true);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
