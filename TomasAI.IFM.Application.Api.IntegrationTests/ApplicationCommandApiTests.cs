using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.Application.CommandParameters;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class ApplicationCommandApiTests(WebApplicationFactory<Program> factory)
        : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task StartApplication_Ok()
    {
        // Arrange
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var applicationApi = new ApplicationCommandApi(commandServiceApi);

        // Act
        var response = await applicationApi.StartApplicationAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ShutdownApplication_Ok()
    {
        // Arrange
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var applicationApi = new ApplicationCommandApi(commandServiceApi);

        // Act
        var response = await applicationApi.ShutdownApplicationAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
