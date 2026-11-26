using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class RecordedBackendBrokerFact : FactAttribute
{
    public RecordedBackendBrokerFact() { if (Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS") != "nats://127.0.0.1:45222") Skip="Requires isolated original-input replay broker on45222."; }
}

public sealed class RecordedMarketDataNatsTests
{
    [RecordedBackendBrokerFact]
    public async Task Real_rpc_preserves_reply_origin_and_captures_exact_exchange_with_physical_deadlines()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var manager=new NatsConnectionManager();
        var client=await manager.GetClientAsync("nats://127.0.0.1:45222",timeout.Token);
        var scope="replay-"+Guid.NewGuid().ToString("N");
        var full="ifm."+scope+".marketdata.v1";
        var at=new DateTimeOffset(2030,1,2,14,30,0,TimeSpan.Zero);
        var origin=new MessageOrigin(at,"original-provider",causationId:Guid.NewGuid());
        var backend=new RecordedMarketDataBackend(new MarketDataReadSnapshot(new(true,new(2030,1,2),[]),[],[],[]));
        var payload=new{Value="original"};
        var original=new MarketDataHostRequest("api/RecordedProbe",MarketDataHostConnection.Encode(payload),Guid.NewGuid(),DateTime.UtcNow.AddSeconds(30));
        var reply=new MarketDataHostReply(MarketDataHostConnection.Encode(new byte[]{4,5,6}),null);
        backend.Accept(new(original,reply,at,at,MessageOriginBinary.Serialize(origin)));
        await using var server=await RecordedMarketDataNatsServer.StartAsync(client,full,backend,timeout.Token);
        var recorder=new Recorder();
        await using var connection=new MarketDataHostConnection(manager,"nats://127.0.0.1:45222",scope,new Clock(at),sessionRecorder:recorder);
        var result=await connection.PostAsync<byte[]>(original.Path,payload,timeout.Token);
        Assert.Equal(new byte[]{4,5,6},result);
        var captured=Assert.Single(recorder.Records);
        Assert.Equal("BackendRpc",captured.Kind);
        var exchange=MarketDataHostConnection.Decode<RecordedMarketDataExchange>(captured.OriginalWire.ToArray());
        Assert.Equal(at,exchange.StartedAtUtc);Assert.Equal(at,exchange.CompletedAtUtc);
        Assert.Equal(origin,MessageOriginBinary.Deserialize(exchange.ReplyOrigin!));
        Assert.NotNull(exchange.RequestOrigin);
        Assert.Equal(original.Payload,exchange.Request.Payload);Assert.Equal(reply.Payload,exchange.Reply.Payload);
        Assert.InRange(exchange.Request.DeadlineUtc,DateTime.UtcNow,DateTime.UtcNow.AddSeconds(31));
        var expired=await client.RequestAsync<MarketDataHostRequest,MarketDataHostReply>(full+".control",original with{DeadlineUtc=DateTime.UtcNow.AddSeconds(-1)},
            requestSerializer:NatsMessagePackSerializer<MarketDataHostRequest>.Default,replySerializer:NatsMessagePackSerializer<MarketDataHostReply>.Default,cancellationToken:timeout.Token);
        Assert.Equal("Market-data request deadline expired.",expired.Data!.Error);
        Assert.Null(global::TomasAI.IFM.Framework.Messaging.Nats.MessageOriginHeaders.Read(expired.Headers));
        recorder.BadReceipt=true;
        backend.Accept(new(original,reply,at,at,MessageOriginBinary.Serialize(origin)));
        await Assert.ThrowsAsync<InvalidDataException>(()=>connection.PostAsync<byte[]>(original.Path,payload,timeout.Token));
    }
    [RecordedBackendBrokerFact]
    public async Task Waiting_original_call_does_not_block_other_keys_or_reuse_consumed_replies()
    {
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(15));await using var manager=new NatsConnectionManager();
        var client=await manager.GetClientAsync("nats://127.0.0.1:45222",stop.Token);var scope="ifm.replay-"+Guid.NewGuid().ToString("N")+".marketdata.v1";
        var backend=new RecordedMarketDataBackend(new MarketDataReadSnapshot(new(true,new(2030,1,2),[]),[],[],[]));
        await using var server=await RecordedMarketDataNatsServer.StartAsync(client,scope,backend,stop.Token);
        var at=new DateTimeOffset(2030,1,2,14,30,0,TimeSpan.Zero);
        var first=new MarketDataHostRequest("api/Probe",[1],Guid.NewGuid(),DateTime.UtcNow.AddSeconds(10));var second=first with{Payload=[2],OperationId=Guid.NewGuid()};
        async Task<NatsMsg<MarketDataHostReply>> Send(MarketDataHostRequest request)=>await client.RequestAsync<MarketDataHostRequest,MarketDataHostReply>(scope+".control",request,requestSerializer:NatsMessagePackSerializer<MarketDataHostRequest>.Default,replySerializer:NatsMessagePackSerializer<MarketDataHostReply>.Default,cancellationToken:stop.Token);
        var firstReply=Send(first);while(backend.Diagnostics.PendingRequests!=1)await Task.Delay(5,stop.Token);
        var secondOrigin=MessageOriginBinary.Serialize(new(at,"second-original"));backend.Accept(new(second,new([22],null),at,at,secondOrigin));
        var secondReply=await Send(second);Assert.Equal(new byte[]{22},secondReply.Data!.Payload);Assert.False(firstReply.IsCompleted);
        var firstOrigin=MessageOriginBinary.Serialize(new(at,"first-original"));backend.Accept(new(first,new([11],null),at,at,firstOrigin));var received=await firstReply;
        Assert.Equal(new byte[]{11},received.Data!.Payload);Assert.Equal(MessageOriginBinary.Deserialize(firstOrigin),global::TomasAI.IFM.Framework.Messaging.Nats.MessageOriginHeaders.Read(received.Headers));
        backend.CompleteTranscript();while(backend.Diagnostics.DeliveringReplies!=0)await Task.Delay(5,stop.Token);backend.VerifyComplete();
        var extra=await Send(first with{OperationId=Guid.NewGuid()});Assert.Contains("UNAVAILABLE",extra.Data!.Error);Assert.Equal(1,backend.Diagnostics.MissingRequests);Assert.Throws<InvalidDataException>(backend.VerifyComplete);
        await Task.WhenAll(server.DisposeAsync().AsTask(),server.DisposeAsync().AsTask());await server.DisposeAsync();
    }
    [RecordedBackendBrokerFact]
    public async Task Missing_original_response_fails_at_eof_and_missing_transport_reply_is_archived_explicitly()
    {
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(15));await using var manager=new NatsConnectionManager();
        var client=await manager.GetClientAsync("nats://127.0.0.1:45222",stop.Token);var suffix="replay-"+Guid.NewGuid().ToString("N");var scope="ifm."+suffix+".marketdata.v1";
        var backend=new RecordedMarketDataBackend(new MarketDataReadSnapshot(new(true,new(2030,1,2),[]),[],[],[]));
        await using(var server=await RecordedMarketDataNatsServer.StartAsync(client,scope,backend,stop.Token))
        {
            var request=new MarketDataHostRequest("api/Probe",[1],Guid.NewGuid(),DateTime.UtcNow.AddSeconds(10));
            async Task<NatsMsg<MarketDataHostReply>> Send()=>await client.RequestAsync<MarketDataHostRequest,MarketDataHostReply>(scope+".control",request,requestSerializer:NatsMessagePackSerializer<MarketDataHostRequest>.Default,replySerializer:NatsMessagePackSerializer<MarketDataHostReply>.Default,cancellationToken:stop.Token);
            var missing=Send();while(backend.Diagnostics.PendingRequests!=1)await Task.Delay(5,stop.Token);backend.CompleteTranscript();
            var failed=await missing;Assert.Contains("UNAVAILABLE",failed.Data!.Error);Assert.Equal(1,backend.Diagnostics.MissingRequests);Assert.Throws<InvalidDataException>(backend.VerifyComplete);
        }
        var recorder=new Recorder();await using var connection=new MarketDataHostConnection(manager,"nats://127.0.0.1:45222",suffix+"-without-server",new Clock(DateTimeOffset.UtcNow),sessionRecorder:recorder);
        await Assert.ThrowsAnyAsync<Exception>(()=>connection.PostAsync<byte[]>("api/Probe",new{Input=1},stop.Token));
        var record=Assert.Single(recorder.Records);var original=MarketDataHostConnection.Decode<RecordedMarketDataExchange>(record.OriginalWire.ToArray());Assert.NotNull(original.TransportFailure);Assert.NotNull(original.RequestOrigin);Assert.Empty(original.Reply.Payload);
        Assert.Throws<IOException>(()=>new RecordedMarketDataBackend(new MarketDataReadSnapshot(new(false,new(2030,1,2),[]),[],[],[])).Accept(original));
    }
    sealed class Clock(DateTimeOffset utc):TimeProvider{public override DateTimeOffset GetUtcNow()=>utc;}
    sealed class Recorder:IRealtimeSessionRecorder
    {
        public bool Enabled=>true;
        public bool BadReceipt;
        public List<RealtimeSessionArchiveRecord> Records{get;}=[];
        public ValueTask<RealtimeSessionArchiveReceipt> RecordAsync(RealtimeSessionArchiveRecord record,CancellationToken cancellationToken=default)
        {Records.Add(record);return ValueTask.FromResult(new RealtimeSessionArchiveReceipt(BadReceipt?"changed":Convert.ToHexStringLower(SHA256.HashData(record.OriginalWire.Span)),"archive",false,"manifest"));}
    }
}
