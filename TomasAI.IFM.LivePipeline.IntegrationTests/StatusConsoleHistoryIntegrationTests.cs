using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Application.EventServices;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.Model;
using TomasAI.IFM.Shared.StatusConsole.Events;
using Xunit;
namespace TomasAI.IFM.LivePipeline.IntegrationTests;
public sealed class StatusConsoleHistoryIntegrationTests
{
    [IsolatedRecoveryBrokerTheory(Timeout=30000)]
    [InlineData(true)]
    public async Task Console_is_live_core_nats_without_replay_or_acknowledgements(bool isolated)
    {
        Assert.True(isolated);
        var url=Environment.GetEnvironmentVariable("IFM_RECOVERY_TEST_NATS_URL")!;Assert.DoesNotContain(":4222",url);
        ActorExtensions.DataSerializer??=new NatsMessagePackDataSerializer();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var connection=new NatsConnectionManager();
        await using var producer=new StatusConsoleEventProducer(new NatsProducerOptions{Url=url},NullLogger.Instance,connection);
        var writer=new StatusConsoleWriter(producer);
        await writer.WriteConsoleAsync(LogSourceType.System,"before subscriber - not retained");
        var client=await connection.GetClientAsync(url,deadline.Token);
        await using var sub=await client.Connection.SubscribeCoreAsync<byte[]>("Notify.StatusConsoleEvent.>",serializer:new NatsByteArrayMessageSerializer(),cancellationToken:deadline.Token);
        await client.Connection.PingAsync(deadline.Token);
        await writer.WriteConsoleAsync(LogSourceType.System,"live console only");
        var message=await sub.Msgs.ReadAsync(deadline.Token);
        var value=ActorExtensions.DataSerializer.Deserialize<StatusConsoleLoggedEvent>(message.Data!)!;
        Assert.Equal("live console only",value.StatusConsoleLog.Message);
        Assert.Null(message.ReplyTo);
        Assert.False(sub.Msgs.TryRead(out _));
    }
}
