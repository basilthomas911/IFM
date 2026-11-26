using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;
public sealed class NatsConnectionManagerReplyModeTests
{
    [Fact]
    public async Task Parameterless_and_Activator_paths_keep_legacy_SharedInbox_and_invalid_mode_is_rejected()
    {
        await using var normal=new NatsConnectionManager();await using var activated=(NatsConnectionManager)Activator.CreateInstance(typeof(NatsConnectionManager))!;
        Assert.Equal(NatsRequestReplyMode.SharedInbox,normal.RequestReplyMode);Assert.Equal(NatsRequestReplyMode.SharedInbox,activated.RequestReplyMode);
        Assert.Throws<ArgumentOutOfRangeException>(()=>new NatsConnectionManager((NatsRequestReplyMode)999));
    }
    [RealtimeBrokerTheory]
    [InlineData(NatsRequestReplyMode.SharedInbox,false)][InlineData(NatsRequestReplyMode.SharedInbox,true)]
    [InlineData(NatsRequestReplyMode.Direct,false)][InlineData(NatsRequestReplyMode.Direct,true)]
    public async Task Actual_mode_preserves_reply_body_original_timestamps_causation_and_legacy_origin(NatsRequestReplyMode mode,bool legacy)
    {
        await using var manager=new NatsConnectionManager(mode);var client=await manager.GetClientAsync(Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")!);Assert.Equal(mode,manager.RequestReplyMode);Assert.Equal(mode,client.Connection.Opts.RequestReplyMode);
        var serializer=new NatsByteArrayMessageSerializer();var subject="ifm.replymode."+Guid.NewGuid().ToString("N");using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var responder=await client.Connection.SubscribeCoreAsync<byte[]>(subject,serializer:serializer,cancellationToken:deadline.Token);await client.PingAsync(deadline.Token);
        var origin=new MessageOrigin(new DateTimeOffset(2026,11,25,14,30,1,TimeSpan.Zero),"original-clock",Guid.NewGuid(),Guid.NewGuid());var causation=Guid.NewGuid().ToString("D");var body=Guid.NewGuid().ToByteArray();
        var headers=legacy?new NatsHeaders{[MessageOriginHeaders.Header]=MessageOriginJson.Serialize(origin)!}:MessageOriginHeaders.Add(null,origin)!;headers["Ifm-Test-Causation"]=causation;
        var request=client.Connection.RequestAsync<byte[],byte[]>(subject,body,headers:headers,requestSerializer:serializer,replySerializer:serializer,replyOpts:new NatsSubOpts{Timeout=TimeSpan.FromSeconds(5)},cancellationToken:deadline.Token).AsTask();
        var incoming=await responder.Msgs.ReadAsync(deadline.Token);Assert.Equal(body,incoming.Data);Assert.Equal(origin,MessageOriginHeaders.Read(incoming.Headers));Assert.Equal(causation,incoming.Headers!["Ifm-Test-Causation"].ToString());
        await incoming.ReplyAsync(incoming.Data,headers:incoming.Headers,serializer:serializer,cancellationToken:deadline.Token);var response=await request;
        Assert.Equal(body,response.Data);Assert.Equal(origin,MessageOriginHeaders.Read(response.Headers));Assert.Equal(causation,response.Headers!["Ifm-Test-Causation"].ToString());
        if(legacy){Assert.True(response.Headers.ContainsKey(MessageOriginHeaders.Header));Assert.False(response.Headers.ContainsKey(MessageOriginHeaders.BinaryHeader));}
    }
    [RealtimeBrokerTheory]
    [InlineData(NatsRequestReplyMode.SharedInbox,false)][InlineData(NatsRequestReplyMode.SharedInbox,true)]
    [InlineData(NatsRequestReplyMode.Direct,false)][InlineData(NatsRequestReplyMode.Direct,true)]
    public async Task Timeout_or_cancellation_releases_original_reply_and_late_reply_cannot_satisfy_next_request(NatsRequestReplyMode mode,bool cancel)
    {
        await using var manager=new NatsConnectionManager(mode);var client=await manager.GetClientAsync(Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")!);var serializer=new NatsByteArrayMessageSerializer();var subject="ifm.replymode."+Guid.NewGuid().ToString("N");using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var responder=await client.Connection.SubscribeCoreAsync<byte[]>(subject,serializer:serializer,cancellationToken:deadline.Token);await client.PingAsync(deadline.Token);
        using var canceled=new CancellationTokenSource();var first=client.Connection.RequestAsync<byte[],byte[]>(subject,new byte[]{1},requestSerializer:serializer,replySerializer:serializer,replyOpts:new NatsSubOpts{Timeout=TimeSpan.FromMilliseconds(cancel?5000:100)},cancellationToken:cancel?canceled.Token:deadline.Token).AsTask();
        var original=await responder.Msgs.ReadAsync(deadline.Token);
        if(cancel){canceled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>first);}else await Assert.ThrowsAsync<NatsNoReplyException>(()=>first);
        await original.ReplyAsync(new byte[]{99},serializer:serializer,cancellationToken:deadline.Token);await client.PingAsync(deadline.Token);
        var second=client.Connection.RequestAsync<byte[],byte[]>(subject,new byte[]{2},requestSerializer:serializer,replySerializer:serializer,replyOpts:new NatsSubOpts{Timeout=TimeSpan.FromSeconds(5)},cancellationToken:deadline.Token).AsTask();
        var next=await responder.Msgs.ReadAsync(deadline.Token);Assert.NotEqual(original.ReplyTo,next.ReplyTo);Assert.Equal(new byte[]{2},next.Data);await next.ReplyAsync(new byte[]{2},serializer:serializer,cancellationToken:deadline.Token);
        Assert.Equal(new byte[]{2},(await second).Data);Assert.True(first.IsCompleted);Assert.False(first.IsCompletedSuccessfully);
    }
}
