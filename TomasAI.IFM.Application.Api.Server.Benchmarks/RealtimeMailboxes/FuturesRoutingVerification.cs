using System.Diagnostics;
using MessagePack;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Npgsql;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;
[MessagePackObject]
public sealed record RoutingSourceEnvelope([property:Key(0)] FuturesTickTradeDataChangedEvent Source,[property:Key(1)] string Dataset,[property:Key(2)] Guid Generation);
public static class FuturesRoutingVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer=new();
    public static async Task Verify()
    {
        await using var infrastructure=new IsolatedIntegrationInfrastructure("futuresrouting",includeCql:false);await infrastructure.StartAsync();
        var cs=new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString){Username=infrastructure.PostgresUser,Password=infrastructure.PostgresPassword}.ConnectionString;await using var pg=new NpgsqlConnection(cs);await pg.OpenAsync();
        await using(var create=new NpgsqlCommand("CREATE TABLE routing_receipts(ordinal bigserial,scenario text NOT NULL,command_id uuid NOT NULL,payload bytea NOT NULL,origin text NOT NULL,source_sequence bigint NOT NULL,trade_id integer NOT NULL,PRIMARY KEY(scenario,command_id));",pg))await create.ExecuteNonQueryAsync();
        foreach(var storage in new[]{StreamConfigStorage.Memory,StreamConfigStorage.File})
        foreach(var mode in new[]{"failure","throw","lost-reply","captured-before-send","completed-no-ack"})
        {
            var name=$"ROUTING_{storage}_{mode.Replace('-','_')}";var subject="routingcase."+name;
            var directory=Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/futures-routing-proof-captures",Guid.NewGuid().ToString("N"));
            await using var connection=new NatsConnection(new NatsOpts{Url=infrastructure.NatsUrl});var js=new NatsJSContext(connection);
            await js.CreateStreamAsync(new StreamConfig(name,[subject]){Storage=storage,Retention=StreamConfigRetention.Workqueue,MaxMsgs=16,MaxBytes=1024*1024,Discard=StreamConfigDiscard.New});
            await js.CreateOrUpdateConsumerAsync(name,new ConsumerConfig("ordered"){FilterSubject=subject,AckPolicy=ConsumerConfigAckPolicy.Explicit,MaxAckPending=1,AckWait=TimeSpan.FromSeconds(3),MaxDeliver=-1});
            foreach(var seq in new[]{1,2})
            {var source=FuturesRoutingDeliveryFixture.Input(seq);(await js.PublishAsync(subject,MessagePackSerializer.Serialize(new RoutingSourceEnvelope(source,source.SourceDataset,source.SourceGenerationId)),serializer:Serializer)).EnsureSuccess();}
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(typeof(FuturesRoutingVerification).Assembly.Location);
            foreach(var arg in new[]{"--futures-routing-crash-child",name,mode,directory})start.ArgumentList.Add(arg);
            start.Environment["IFM_ROUTING_TEST_NATS"]=infrastructure.NatsUrl;start.Environment["IFM_ROUTING_TEST_PG"]=cs;
            using var child=Process.Start(start)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
            try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));}catch{child.Kill(true);throw;}
            Require(child.ExitCode==0&&(await stdout).Contains("ROUTING_INPUT_RETAINED"),"Child failed: "+await stderr);
            await using var recovered=new FuturesRoutingDeliveryFixture(directory);Bind(recovered,pg,name,"none");await recovered.Start();
            var expectedRecovery=mode=="completed-no-ack"?0:mode=="captured-before-send"?2:1;
            Require(recovered.Attempts.Count==expectedRecovery,"Confirmed sibling was resent or pending branch lost.");
            var consumer=await js.GetConsumerAsync(name,"ordered");
            foreach(var seq in new[]{1,2})
            {
                var input=await Fetch(consumer);var source=Decode(input.Data!);Require(source.Id==FuturesRoutingDeliveryFixture.Input(seq).Id,"Source order changed.");
                Require(await recovered.Deliver(source),"Recovery failed.");await input.AckAsync(new AckOpts{DoubleAck=true});
            }
            await consumer.RefreshAsync();Require(consumer.Info.NumPending==0&&consumer.Info.NumAckPending==0,"Unsettled source.");
            await using(var read=new NpgsqlCommand("SELECT source_sequence,trade_id FROM routing_receipts WHERE scenario=@scenario ORDER BY ordinal",pg))
            {read.Parameters.AddWithValue("scenario",name);await using var rows=await read.ExecuteReaderAsync();var ordered=new List<string>();while(await rows.ReadAsync())ordered.Add($"{rows.GetInt64(0)}:{rows.GetInt32(1)}");Require(ordered.SequenceEqual(new[]{"1:1","1:2","2:1","2:2"}),"Branches duplicated or reordered.");}
            Require(recovered.Journal.Pending().Length==0,"Pending journal not drained.");
            Console.WriteLine($"PASS {storage}/{mode}: four unique PostgreSQL surrogate receipts in 1:A,1:B,2:A,2:B order; identical command bytes/origin; startup resumed {expectedRecovery} branches; no source pending");
            await js.DeleteStreamAsync(name);
        }
        Console.WriteLine("Actual Futures router actor/scheduler/index and durable binary routing journal. Position receiver substituted by PostgreSQL idempotent command ledger. Broker stays up through abrupt API-child exit. Source headers represented by explicit test envelope. No broker restart/power-loss, real position financial mutation, live activation, multi-host ownership or native epoch fencing qualification.");
    }
    static void Bind(FuturesRoutingDeliveryFixture fixture,NpgsqlConnection pg,string scenario,string mode)
    {
        fixture.Send=async command=>
        {
            if(mode=="captured-before-send") {Console.WriteLine("ROUTING_INPUT_RETAINED");Environment.Exit(0);}
            var failed=command.EntityId.Trade.TradeId==2;
            if(failed&&mode=="failure")return new ServiceFailed<Guid>(1,"Rejected branch B");
            if(failed&&mode=="throw")throw new IOException("Failed branch B");
            var bytes=MessagePackSerializer.Serialize(command);var origin=MessageOriginBinary.Serialize(MessageOrigins.Get(command)!);
            await using(var insert=new NpgsqlCommand("INSERT INTO routing_receipts(scenario,command_id,payload,origin,source_sequence,trade_id) VALUES(@scenario,@id,@payload,@origin,@sequence,@trade) ON CONFLICT DO NOTHING",pg))
            {insert.Parameters.AddWithValue("scenario",scenario);insert.Parameters.AddWithValue("id",command.CommandId);insert.Parameters.AddWithValue("payload",bytes);insert.Parameters.AddWithValue("origin",origin);insert.Parameters.AddWithValue("sequence",command.SourceSequence);insert.Parameters.AddWithValue("trade",command.EntityId.Trade.TradeId);await insert.ExecuteNonQueryAsync();}
            await using(var read=new NpgsqlCommand("SELECT payload,origin FROM routing_receipts WHERE scenario=@scenario AND command_id=@id",pg))
            {read.Parameters.AddWithValue("scenario",scenario);read.Parameters.AddWithValue("id",command.CommandId);await using var rows=await read.ExecuteReaderAsync();await rows.ReadAsync();Require(((byte[])rows[0]).AsSpan().SequenceEqual(bytes)&&rows.GetString(1)==origin,"Retry command bytes or origin changed.");}
            if(failed&&mode=="lost-reply")throw new IOException("Lost branch B reply after acceptance");
            return new ServiceOk<Guid>(command.CommandId);
        };
    }
    public static async Task CrashChild(string name,string mode,string directory)
    {
        var pg=new NpgsqlConnection(Environment.GetEnvironmentVariable("IFM_ROUTING_TEST_PG"));await pg.OpenAsync();var fixture=new FuturesRoutingDeliveryFixture(directory);Bind(fixture,pg,name,mode);await fixture.Start();
        var connection=new NatsConnection(new NatsOpts{Url=Environment.GetEnvironmentVariable("IFM_ROUTING_TEST_NATS")!});var consumer=await new NatsJSContext(connection).GetConsumerAsync(name,"ordered");
        var input=await Fetch(consumer);var source=Decode(input.Data!);Require(await fixture.Deliver(source)==(mode=="completed-no-ack"),"Unexpected settlement.");
        await foreach(var probe in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts{MaxMsgs=1,Expires=TimeSpan.FromMilliseconds(1100)},Serializer))Require(Decode(probe.Data!).Id==source.Id,"Successor overtook failed source.");
        Console.WriteLine("ROUTING_INPUT_RETAINED");Environment.Exit(0);
    }
    static FuturesTickTradeDataChangedEvent Decode(byte[] bytes){var envelope=MessagePackSerializer.Deserialize<RoutingSourceEnvelope>(bytes);return envelope.Source with{SourceDataset=envelope.Dataset,SourceGenerationId=envelope.Generation};}
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {await foreach(var input in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts{MaxMsgs=1,Expires=TimeSpan.FromSeconds(5)},Serializer))return input;throw new TimeoutException("Expected source not redelivered.");}
    static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
}
