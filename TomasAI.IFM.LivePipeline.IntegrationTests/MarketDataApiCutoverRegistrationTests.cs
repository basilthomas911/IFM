using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Application.Api.Server.Core.DependencyInjection;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using Xunit;
namespace TomasAI.IFM.LivePipeline.IntegrationTests;
public sealed class MarketDataApiCutoverRegistrationTests
{
    [Theory]
    [InlineData("Futures")]
    [InlineData("Options")]
    public void Dataset_recovery_error_preserves_UI_error_message_operation_and_dataset(string role)
    {
        var status = new MarketDataDatasetStatus(role, "GLBX.MDP3", Guid.NewGuid(), Guid.NewGuid(), 123, false, false,
            "CleanupUnconfirmed", "Recovery deadline of 120.000s expired during reset/readiness.", new(2026, 10, 21), Guid.NewGuid());
        var failed = OptionFeedRecoveryNotificationService.CreateDatasetEvent(status);
        var bytes = MessagePack.MessagePackSerializer.Serialize(failed);
        var decoded = MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataDatasetRecoveryFailedEvent>(bytes);
        Assert.Equal(status.OperationId, decoded.CommandId);
        Assert.Equal(status.Dataset, decoded.Dataset);
        Assert.Equal(role, decoded.Role);
        Assert.Contains("120.000s", decoded.ErrorMessage);
        Assert.Contains("reset/readiness", decoded.ErrorMessage);
        Assert.Equal(503, decoded.ErrorCode);
        Assert.Equal("DatasetRecoveryFailed", decoded.Subject.Verb);
    }

    [Fact]
    public async Task Remote_API_composition_has_one_client_and_no_provider_lifecycle_or_worker_owner()
    {
        var services = new ServiceCollection(); services.AddSingleton<NatsConnectionManager>();
        var config = new ConfigurationManager();
        services.AddSingleton<TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts.INatsProducerOptions>(new TomasAI.IFM.Framework.Messaging.NatsJetStream.NatsProducerOptions { Url = "nats://localhost:4222" });
        typeof(CoreServiceRegistration).GetMethod("RegisterRemoteMarketDataRuntimeServices", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [services, config]);
        Assert.Single(services, x => x.ServiceType == typeof(IMarketDataApi));
        Assert.DoesNotContain(services, x => x.ServiceType.Name is "DatabentoMarketDataApi" or "IDatabentoMarketDataEpochFactory" or "DatabentoMarketDataWatchdogService" or "DatasetWorkerProcessRecoveryService" or "QualifiedCompositionDiscovery" or "IMarketDataLifecycleRequests");
        Assert.Contains(services, x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(OptionFeedRecoveryNotificationService));
        await using var provider = services.BuildServiceProvider();
        Assert.IsType<MarketDataApiClient>(provider.GetRequiredService<IMarketDataApi>());
        Assert.IsType<MarketDataRemoteAdmission>(provider.GetRequiredService<TomasAI.IFM.Shared.EventModelActor.Contracts.IRealtimeSourceAdmission>());
        var core = await provider.GetRequiredService<NatsConnectionManager>().GetClientAsync("nats://localhost:4222", default);
        Assert.Same(core, await provider.GetRequiredService<TomasAI.IFM.Application.MarketData.Databento.Workers.MarketDataHostConnection>().GetClientAsync(default));
    }
}
