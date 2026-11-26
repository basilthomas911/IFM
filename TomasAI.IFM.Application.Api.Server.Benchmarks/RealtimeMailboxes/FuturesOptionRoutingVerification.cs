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
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;
[MessagePackObject]
public sealed record OptionRoutingSourceEnvelope([property:Key(0)] FuturesTickTradeDataChangedEvent Source,[property:Key(1)] string Dataset,[property:Key(2)] Guid Generation);
public static class FuturesOptionRoutingVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer=new();
    public static async Task Verify()
    {
        await using var infrastructure=new IsolatedIntegrationInfrastructure("optionrouting",includeCql:false);await infrastructure.StartAsync();
        var cs=new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString){Username=infrastructure.PostgresUser,Password=infrastructure.PostgresPassword}.ConnectionString;await using var pg=new NpgsqlConnection(cs);await pg.OpenAsync();
        await using(var create=new NpgsqlCommand("CREATE TABLE option_publications(ordinal bigserial,scenario text NOT NULL,event_id uuid NOT NULL,payload bytea NOT NULL,origin text NOT NULL,source_sequence bigint NOT NULL,trade_id integer NOT NULL,PRIMARY KEY(scenario,event_id)); CREATE TABLE option_routing_receipts(ordinal bigserial,scenario text NOT NULL,command_id uuid NOT NULL,payload bytea NOT NULL,origin text NOT NULL,source_sequence bigint NOT NULL,trade_id integer NOT NULL,PRIMARY KEY(scenario,command_id));",pg))await create.ExecuteNonQueryAsync();
        foreach(var kind in new[]{"Quote","Pricing"})
        foreach(var storage in new[]{StreamConfigStorage.Memory,StreamConfigStorage.File})
        foreach(var mode in new[]{"failure","throw","lost-reply","captured-before-send","completed-no-ack","publication-failure","publication-lost-reply","before-publication"})
        {
            var name=$"OPTION_ROUTING_{kind}_{storage}_{mode.Replace('-','_')}";var subject="optionroutingcase."+name;
            var directory=Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/option-routing-proof-captures",Guid.NewGuid().ToString("N"));
            await using var connection=new NatsConnection(new NatsOpts{Url=infrastructure.NatsUrl});var js=new NatsJSContext(connection);
            await js.CreateStreamAsync(new StreamConfig(name,[subject]){Storage=storage,Retention=StreamConfigRetention.Workqueue,MaxMsgs=16,MaxBytes=1024*1024,Discard=StreamConfigDiscard.New});
            await js.CreateOrUpdateConsumerAsync(name,new ConsumerConfig("ordered"){FilterSubject=subject,AckPolicy=ConsumerConfigAckPolicy.Explicit,MaxAckPending=1,AckWait=TimeSpan.FromSeconds(3),MaxDeliver=-1});
            foreach(var seq in new[]{1,2})
            {var source=FuturesOptionRoutingDeliveryFixture.Input(seq,kind);(await js.PublishAsync(subject,MessagePackSerializer.Serialize(new OptionRoutingSourceEnvelope(source,source.SourceDataset,source.SourceGenerationId)),serializer:Serializer)).EnsureSuccess();}
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(typeof(FuturesOptionRoutingVerification).Assembly.Location);
            foreach(var arg in new[]{"--option-routing-crash-child",name,mode,directory})start.ArgumentList.Add(arg);
            start.Environment["IFM_OPTION_ROUTING_TEST_NATS"]=infrastructure.NatsUrl;start.Environment["IFM_OPTION_ROUTING_TEST_PG"]=cs;
            start.Environment["IFM_OPTION_ROUTING_KIND"]=kind;
            using var child=Process.Start(start)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
            try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));}catch{child.Kill(true);throw;}
            Require(child.ExitCode==0&&(await stdout).Contains("OPTION_ROUTING_INPUT_RETAINED"),"Child failed: "+await stderr);
            await using var recovered=new FuturesOptionRoutingDeliveryFixture(directory);recovered.Time.Now+=TimeSpan.FromDays(1);Bind(recovered,pg,name,"none");await recovered.Start();
            var expectedRecovery=mode=="completed-no-ack"?0:mode is "captured-before-send" or "publication-failure" or "publication-lost-reply" or "before-publication"?2:1;
            var expectedPublications=mode=="before-publication"?2:mode is "publication-failure" or "publication-lost-reply"?1:0;
            Require(recovered.Publications.Count==expectedPublications,"Snapshot branch recovery repeated or lost a confirmed publication.");
            Require(recovered.Attempts.Count==expectedRecovery,"Confirmed sibling was resent or pending branch lost.");
            var consumer=await js.GetConsumerAsync(name,"ordered");
            foreach(var seq in new[]{1,2})
            {
                var input=await Fetch(consumer);var source=Decode(input.Data!);Require(source.Id==FuturesOptionRoutingDeliveryFixture.Input(seq,kind).Id,"Source order changed.");
                Require(await recovered.Deliver(source),"Recovery failed.");await input.AckAsync(new AckOpts{DoubleAck=true});
            }
            await consumer.RefreshAsync();Require(consumer.Info.NumPending==0&&consumer.Info.NumAckPending==0,"Unsettled source.");
            await using(var read=new NpgsqlCommand("SELECT source_sequence,trade_id FROM option_routing_receipts WHERE scenario=@scenario ORDER BY ordinal",pg))
            {read.Parameters.AddWithValue("scenario",name);await using var rows=await read.ExecuteReaderAsync();var ordered=new List<string>();while(await rows.ReadAsync())ordered.Add($"{rows.GetInt64(0)}:{rows.GetInt32(1)}");Require(ordered.SequenceEqual(new[]{"1:1","1:2","2:1","2:2"}),"Branches duplicated or reordered.");}
            await using(var read=new NpgsqlCommand("SELECT source_sequence,trade_id FROM option_publications WHERE scenario=@scenario ORDER BY ordinal",pg))
            {read.Parameters.AddWithValue("scenario",name);await using var rows=await read.ExecuteReaderAsync();var ordered=new List<string>();while(await rows.ReadAsync())ordered.Add($"{rows.GetInt64(0)}:{rows.GetInt32(1)}");Require(ordered.SequenceEqual(new[]{"1:1","1:2","2:1","2:2"}),"Snapshot notifications duplicated or reordered.");}
            Require(recovered.Journal.Pending().Length==0,"Pending journal not drained.");
            Console.WriteLine($"PASS {kind}/{storage}/{mode}: four unique command and four snapshot PostgreSQL surrogate receipts in 1:A,1:B,2:A,2:B order; identical command bytes/origin; startup resumed {expectedRecovery} commands/{expectedPublications} publications; no source pending");
            await js.DeleteStreamAsync(name);
        }
        Console.WriteLine("Actual FuturesOption router actor/scheduler/index, cache and durable binary option routing journal. Position receiver and notification acceptance substituted by PostgreSQL idempotent ledgers; display mirrors/financial mutation are not simulated. Broker stays up through abrupt API-child exit. Source headers represented by explicit test envelope. No broker restart/power-loss, real position financial mutation, live activation, multi-host ownership or native epoch fencing qualification.");
    }
    static void Bind(FuturesOptionRoutingDeliveryFixture fixture,NpgsqlConnection pg,string scenario,string mode)
    {
        fixture.Publish=async publication=>
        {
            if(mode=="before-publication"){Console.WriteLine("OPTION_ROUTING_INPUT_RETAINED");Environment.Exit(0);}
            var failed=publication.EntityId.TradeId==2;
            if(failed&&mode=="publication-failure")throw new IOException("Snapshot branch B rejected.");
            var bytes=MessagePackSerializer.Serialize(publication);var origin=MessageOriginBinary.Serialize(MessageOrigins.Get(publication)!);
            await using(var insert=new NpgsqlCommand("INSERT INTO option_publications(scenario,event_id,payload,origin,source_sequence,trade_id) VALUES(@scenario,@id,@payload,@origin,@sequence,@trade) ON CONFLICT DO NOTHING",pg))
            {insert.Parameters.AddWithValue("scenario",scenario);insert.Parameters.AddWithValue("id",publication.Id);insert.Parameters.AddWithValue("payload",bytes);insert.Parameters.AddWithValue("origin",origin);insert.Parameters.AddWithValue("sequence",publication.Snapshot.Legs[0].Observation!.TickId);insert.Parameters.AddWithValue("trade",publication.EntityId.TradeId);await insert.ExecuteNonQueryAsync();}
            await using(var read=new NpgsqlCommand("SELECT payload,origin FROM option_publications WHERE scenario=@scenario AND event_id=@id",pg))
            {read.Parameters.AddWithValue("scenario",scenario);read.Parameters.AddWithValue("id",publication.Id);await using var rows=await read.ExecuteReaderAsync();await rows.ReadAsync();Require(((byte[])rows[0]).AsSpan().SequenceEqual(bytes)&&rows.GetString(1)==origin,"Retry notification bytes or origin changed.");}
            if(failed&&mode=="publication-lost-reply")throw new IOException("Snapshot B accepted but reply lost.");
        };
        fixture.Send=async command=>
        {
            if(mode=="captured-before-send") {Console.WriteLine("OPTION_ROUTING_INPUT_RETAINED");Environment.Exit(0);}
            var failed=command.EntityId.Trade.TradeId==2;
            if(failed&&mode=="failure")return new ServiceFailed<Guid>(1,"Rejected branch B");
            if(failed&&mode=="throw")throw new IOException("Failed branch B");
            var bytes=MessagePackSerializer.Serialize(command);var origin=MessageOriginBinary.Serialize(MessageOrigins.Get(command)!);
            await using(var insert=new NpgsqlCommand("INSERT INTO option_routing_receipts(scenario,command_id,payload,origin,source_sequence,trade_id) VALUES(@scenario,@id,@payload,@origin,@sequence,@trade) ON CONFLICT DO NOTHING",pg))
            {insert.Parameters.AddWithValue("scenario",scenario);insert.Parameters.AddWithValue("id",command.CommandId);insert.Parameters.AddWithValue("payload",bytes);insert.Parameters.AddWithValue("origin",origin);insert.Parameters.AddWithValue("sequence",command.SourceSequence);insert.Parameters.AddWithValue("trade",command.EntityId.Trade.TradeId);await insert.ExecuteNonQueryAsync();}
            await using(var read=new NpgsqlCommand("SELECT payload,origin FROM option_routing_receipts WHERE scenario=@scenario AND command_id=@id",pg))
            {read.Parameters.AddWithValue("scenario",scenario);read.Parameters.AddWithValue("id",command.CommandId);await using var rows=await read.ExecuteReaderAsync();await rows.ReadAsync();Require(((byte[])rows[0]).AsSpan().SequenceEqual(bytes)&&rows.GetString(1)==origin,"Retry command bytes or origin changed.");}
            if(failed&&mode=="lost-reply")throw new IOException("Lost branch B reply after acceptance");
            return new ServiceOk<Guid>(command.CommandId);
        };
    }
    public static async Task CrashChild(string name,string mode,string directory)
    {
        var pg=new NpgsqlConnection(Environment.GetEnvironmentVariable("IFM_OPTION_ROUTING_TEST_PG"));await pg.OpenAsync();var fixture=new FuturesOptionRoutingDeliveryFixture(directory);Bind(fixture,pg,name,mode);await fixture.Start();
        var connection=new NatsConnection(new NatsOpts{Url=Environment.GetEnvironmentVariable("IFM_OPTION_ROUTING_TEST_NATS")!});var consumer=await new NatsJSContext(connection).GetConsumerAsync(name,"ordered");
        var input=await Fetch(consumer);var source=Decode(input.Data!);Require(await fixture.Deliver(source)==(mode=="completed-no-ack"),"Unexpected settlement.");
        await foreach(var probe in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts{MaxMsgs=1,Expires=TimeSpan.FromMilliseconds(1100)},Serializer))Require(Decode(probe.Data!).Id==source.Id,"Successor overtook failed source.");
        Console.WriteLine("OPTION_ROUTING_INPUT_RETAINED");Environment.Exit(0);
    }
    static FuturesTickTradeDataChangedEvent Decode(byte[] bytes){var envelope=MessagePackSerializer.Deserialize<OptionRoutingSourceEnvelope>(bytes);return envelope.Source with{SourceDataset=envelope.Dataset,SourceGenerationId=envelope.Generation};}
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {await foreach(var input in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts{MaxMsgs=1,Expires=TimeSpan.FromSeconds(5)},Serializer))return input;throw new TimeoutException("Expected source not redelivered.");}
    static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
}
