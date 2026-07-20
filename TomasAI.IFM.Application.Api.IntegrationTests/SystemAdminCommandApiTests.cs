using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

/// <summary>
/// Integration tests for the SystemAdmin command API.
/// </summary>
public class SystemAdminCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Tests backing up a database via the SystemAdmin command API.
    /// </summary>
    [Fact]
    public async Task BackupDatabase_Ok()
    {
        // Arrange
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var systemAdminApi = new SystemAdminCommandApi(commandServiceApi);

        // Act
        var response = await systemAdminApi.BackupDatabaseAsync("EventDb", DatabaseBackupType.Full, 60);

        // Assert
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
