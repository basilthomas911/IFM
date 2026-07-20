using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Shared.SystemAdmin.Queries;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

/// <summary>
/// Integration tests for the SystemAdmin query API.
/// </summary>
public class SystemAdminQueryApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Tests retrieval of database names.
    /// </summary>
    [Fact]
    public async Task GetDatabaseNames_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));

        // act
        var response = await queryServiceApi.ExecuteQueryAsync(SystemAdminQueryUriPath.GetDatabaseNames, new GetDatabaseNamesQuery());

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<DatabaseNamesReadModel>();
    }
}
