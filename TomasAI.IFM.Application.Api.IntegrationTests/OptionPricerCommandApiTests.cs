using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class OptionPricerCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task InsertSpreadDistributions_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionPricerCommandApi(commandServiceApi);
        var now = DateTime.Now;
        var valueDate = DateOnly.FromDateTime(now);
        var putSpread = new SpreadDistributionReadModel(1, 1, valueDate, TradeType.PutCreditSpread, TradeStatus.Open, 10, 100.0, 0.1, 10m, 1, 0.2, 0.3, 0.5, now);
        var callSpread = new SpreadDistributionReadModel(2, 2, valueDate, TradeType.CallCreditSpread, TradeStatus.Open, 10, 100.0, 0.1, 10m, 1, 0.2, 0.3, 0.5, now);
        var response = await api.InsertSpreadDistributionsAsync(putSpread, callSpread);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task SubmitSpreadDistributionJob_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionPricerCommandApi(commandServiceApi);
        var now = DateTime.Now;
        var valueDate = DateOnly.FromDateTime(now);
        var job = new SpreadDistributionJobReadModel(1, 1, TradeType.CallCreditSpread, TradeStatus.Open, valueDate, 10, now, SpreadDistributionJobStatus.InProgress, null, null, true, 0.1);
        var response = await api.SubmitSpreadDistributionJobAsync(job);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CompleteSpreadDistributionJob_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionPricerCommandApi(commandServiceApi);
        var now = DateTime.Now;
        var entityId = new SpreadDistributionJobEntityId(1, 1, DateOnly.FromDateTime(now));
        var jobCompleted = now;
        var jobStatus = SpreadDistributionJobStatus.Completed;
        var response = await api.CompleteSpreadDistributionJobAsync(entityId, jobCompleted, jobStatus);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task FailSpreadDistributionJob_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionPricerCommandApi(commandServiceApi);
        var now = DateTime.Now;
        var entityId = new SpreadDistributionJobEntityId(1, 1, DateOnly.FromDateTime(now));
        var jobFailed = now;
        var jobStatus = SpreadDistributionJobStatus.Failed;
        var errorMessage = "Test error";
        var response = await api.FailSpreadDistributionJobAsync(entityId, jobFailed, jobStatus, errorMessage);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ClearSpreadDistributionJob_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionPricerCommandApi(commandServiceApi);
        var entityId = new SpreadDistributionJobEntityId(1, 1, new DateOnly(2024, 1, 1));
        var response = await api.ClearSpreadDistributionJobAsync(entityId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeleteSpreadDistributionJobsInProgress_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionPricerCommandApi(commandServiceApi);
        var entityId = new SpreadDistributionJobEntityId(1, 1, new DateOnly(2024, 1, 1));
        var response = await api.DeleteSpreadDistributionJobsInProgressAsync(entityId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
