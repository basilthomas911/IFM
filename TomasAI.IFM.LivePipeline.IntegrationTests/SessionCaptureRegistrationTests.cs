using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using SimpleInjector;
using TomasAI.IFM.Application.Api.Server.Core.DependencyInjection;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class SessionCaptureRegistrationTests
{
    static ConfigurationManager Configuration(bool enabled, string root)
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string,string?> { ["Actors:SessionCapture:Enabled"] = enabled.ToString(),
            ["Actors:SessionCapture:Directory"] = root, ["Actors:SessionCapture:SessionId"] = "isolated-di" });
        return config;
    }
    static ServiceCollection Services()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<NatsConnectionManager>();
        services.AddSingleton<INatsProducerOptions>(new NatsProducerOptions { Url = "nats://127.0.0.1:1" });
        services.AddSingleton<INatsJetStreamProducerOptions>(new NatsJetStreamProducerOptions { Url = "nats://127.0.0.1:1" });
        var capture = Substitute.For<IRealtimePublicationCapture>(); capture.Enabled.Returns(false); services.AddSingleton(capture);
        return services;
    }
    static IHostEnvironment Environment(string name)
    { var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns(name); return environment; }

    [Fact]
    public async Task Disabled_default_creates_no_archive_and_keeps_the_original_producer_composition()
    {
        var path = Path.Combine(Path.GetTempPath(), "ifm-session-disabled", Guid.NewGuid().ToString("N"));
        var services = Services(); using var container = new Container();
        SessionCaptureServiceRegistration.Register(services, Configuration(false,path), Environment("Production"), container, new());
        await using var provider = services.BuildServiceProvider();
        var recorder = provider.GetRequiredService<IRealtimeSessionRecorder>();
        Assert.False(recorder.Enabled); Assert.Same(recorder, provider.GetRequiredService<DurableRealtimeSessionArchive>());
        Assert.IsType<NatsActorProducer>(provider.GetRequiredService<IActorProducer>()); Assert.False(Directory.Exists(path));
    }
    [Fact]
    public async Task Development_resolves_one_real_archive_without_starting_the_durable_mailbox_or_a_broker_connection()
    {
        var path = Path.Combine(Path.GetTempPath(), "ifm-session-di", Guid.NewGuid().ToString("N"));
        var services = Services(); using var container = new Container();
        SessionCaptureServiceRegistration.Register(services, Configuration(true,path), Environment("Development"), container,
            new() { Enabled = false, CapacityQualified = false, Directory = path + "-mailboxes" });
        await using var provider = services.BuildServiceProvider(); var archive = provider.GetRequiredService<DurableRealtimeSessionArchive>();
        Assert.True(archive.Enabled); Assert.Same(archive, provider.GetRequiredService<IRealtimeSessionRecorder>());
        Assert.False(provider.GetRequiredService<IRealtimePublicationCapture>().Enabled);
        var producer = Assert.IsType<NatsActorProducer>(provider.GetRequiredService<IActorProducer>()); Assert.False(producer.IsRunning);
        Assert.False(Assert.IsType<NatsJetStreamActorProducer>(provider.GetRequiredService<IJSActorProducer>()).IsRunning);
        Assert.Equal(0, archive.PendingRecords); Assert.Empty(archive.ReadCommitted());
    }
    [Theory]
    [InlineData("Production",false,false)]
    [InlineData("Development",true,false)]
    [InlineData("Development",false,true)]
    public void Unsupported_environment_enabled_backend_or_same_owner_directory_is_rejected(string environment, bool backend, bool sameDirectory)
    {
        var path = Path.Combine(Path.GetTempPath(), "ifm-session-refuse", Guid.NewGuid().ToString("N")); using var container = new Container();
        Assert.Throws<InvalidOperationException>(() => SessionCaptureServiceRegistration.Register(Services(), Configuration(true,path), Environment(environment),
            container, new() { Enabled = backend, Directory = sameDirectory ? path : path + "-mailboxes" }));
        Assert.False(Directory.Exists(path));
    }
}
