using System.Diagnostics;
using MessagePack;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Npgsql;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.TestSupport;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;
[MessagePackObject]
public sealed record BrokerQuoteReceipt([property:Key(0)] string Contract,[property:Key(1)] decimal Bid,[property:Key(2)] decimal Ask,
    [property:Key(3)] int BidSize,[property:Key(4)] int AskSize,[property:Key(5)] DateTime MarketTimeUtc,[property:Key(6)] long Epoch,[property:Key(7)] long Sequence);
public static class BrokerOrderSettlementVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer=new();
    static byte[] QuoteBytes(BrokerMarketQuote quote)=>MessagePackSerializer.Serialize(new BrokerQuoteReceipt(quote.ContractId,quote.Bid,quote.Ask,quote.BidSize,quote.AskSize,quote.MarketTimeUtc,quote.SourceEpoch,quote.SourceSequence));
    public static async Task Verify()
    {
        await using var infrastructure=new IsolatedIntegrationInfrastructure("brokerordersettlement",includeCql:false);await infrastructure.StartAsync();
        var cs=new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString){Username=infrastructure.PostgresUser,Password=infrastructure.PostgresPassword}.ConnectionString;
        await using var pg=new NpgsqlConnection(cs);await pg.OpenAsync();
        await using(var create=new NpgsqlCommand("CREATE TABLE broker_quote_receipts(ordinal bigserial,scenario text NOT NULL,contract_id text NOT NULL,epoch bigint NOT NULL,source_sequence bigint NOT NULL,payload bytea NOT NULL,PRIMARY KEY(scenario,contract_id,epoch,source_sequence));",pg))await create.ExecuteNonQueryAsync();
        foreach(var kind in new[]{"Futures","Option"})
        foreach(var storage in new[]{StreamConfigStorage.Memory,StreamConfigStorage.File})
        foreach(var mode in new[]{"throw","lost-reply","completed-no-ack"})
        {
            var name=$"BROKER_{kind}_{storage}_{mode.Replace('-','_')}";var subject="brokerquotecase."+name;
            await using var connection=new NatsConnection(new NatsOpts{Url=infrastructure.NatsUrl});var js=new NatsJSContext(connection);
            await js.CreateStreamAsync(new StreamConfig(name,[subject]){Storage=storage,Retention=StreamConfigRetention.Workqueue,MaxMsgs=16,MaxBytes=1024*1024,Discard=StreamConfigDiscard.New});
            await js.CreateOrUpdateConsumerAsync(name,new ConsumerConfig("ordered"){FilterSubject=subject,AckPolicy=ConsumerConfigAckPolicy.Explicit,MaxAckPending=1,AckWait=TimeSpan.FromSeconds(3),MaxDeliver=-1});
            foreach(var seq in new[]{1,2})(await js.PublishAsync(subject,BrokerOrderDeliveryFixture.Encode(BrokerOrderDeliveryFixture.Input(kind,seq)),serializer:Serializer)).EnsureSuccess();
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(typeof(BrokerOrderSettlementVerification).Assembly.Location);
            foreach(var arg in new[]{"--broker-order-settlement-crash-child",name,kind,mode})start.ArgumentList.Add(arg);
            start.Environment["IFM_BROKER_TEST_NATS"]=infrastructure.NatsUrl;start.Environment["IFM_BROKER_TEST_PG"]=cs;
            using var child=Process.Start(start)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
            try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));}catch{child.Kill(true);throw;}
            Require(child.ExitCode==0&&(await stdout).Contains("BROKER_INPUT_RETAINED"),"Child failed: "+await stderr);
            await using var recovered=new BrokerOrderDeliveryFixture();Bind(recovered,pg,name);await recovered.Start();var consumer=await js.GetConsumerAsync(name,"ordered");
            foreach(var seq in new[]{1,2})
            {
                var input=await Fetch(consumer);var source=BrokerOrderDeliveryFixture.Decode(kind,input.Data!);
                try
                {
                    Require(source.Id==BrokerOrderDeliveryFixture.Input(kind,seq).Id,"Source identity/order changed.");
                    Require(await recovered.Deliver(source),"Retained quote retry failed.");
                    await input.AckAsync(new AckOpts{DoubleAck=true});
                }
                finally{if(source is FuturesTickQuoteDataChangedEvent q)q.QuoteData.Dispose();}
            }
            await consumer.RefreshAsync();Require(consumer.Info.NumPending==0&&consumer.Info.NumAckPending==0,"Unsettled broker source.");
            await using(var read=new NpgsqlCommand("SELECT source_sequence FROM broker_quote_receipts WHERE scenario=@scenario ORDER BY ordinal",pg))
            {read.Parameters.AddWithValue("scenario",name);await using var rows=await read.ExecuteReaderAsync();var sequences=new List<long>();while(await rows.ReadAsync())sequences.Add(rows.GetInt64(0));Require(sequences.SequenceEqual(new long[]{1,2}),"Quote receipts duplicated or reordered.");}
            Console.WriteLine($"PASS {kind}/{storage}/{mode}: actual actor/scheduler; original market time, prices, sizes, epoch and sequence; two PostgreSQL surrogate quote receipts in order; no pending input");
            await js.DeleteStreamAsync(name);
        }
        var path=Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/broker-order-save-failure",Guid.NewGuid().ToString("N"),"ledger.json");
        var proof=await BrokerEmulatorSettlementFixture.AuditSaveFailure(path);
        Require(!proof.FirstSettled&&proof.RetrySettled&&proof.MemoryExecutions==0&&proof.RestoredExecutions==1&&proof.RestoredCash<proof.StartingCash&&proof.MemoryCash==proof.StartingCash&&proof.RetryCheckpointWrites==1&&proof.PendingReadsBlocked,"Expected financial checkpoint recovery was not verified.");
        Console.WriteLine($"RECOVERY_FIXED Emulator checkpoint: first-settled={proof.FirstSettled}; retry-settled={proof.RetrySettled}; starting-cash={proof.StartingCash}; memory-cash={proof.MemoryCash}; restored-cash={proof.RestoredCash}; memory-executions={proof.MemoryExecutions}; restored-executions={proof.RestoredExecutions}; retry-checkpoint-writes={proof.RetryCheckpointWrites}");
        Console.WriteLine("Restart driver uses a PostgreSQL idempotent quote-acceptance substitute, not production broker financial settlement. Memory/File broker stays up through API-child exit; source ownership is external to the existing Core/local queue. Actual application emulator/file checkpoint separately verifies failed-save recovery; end-to-end financial retries, partial fills, callback publication, broker/power loss and native generation fencing remain unqualified. No live restart or backend cutover.");
    }
    static void Bind(BrokerOrderDeliveryFixture fixture,NpgsqlConnection pg,string scenario)
    {
        fixture.Publish=async quote=>
        {
            var bytes=QuoteBytes(quote);
            await using(var insert=new NpgsqlCommand("INSERT INTO broker_quote_receipts(scenario,contract_id,epoch,source_sequence,payload) VALUES(@scenario,@contract,@epoch,@sequence,@payload) ON CONFLICT DO NOTHING",pg))
            {insert.Parameters.AddWithValue("scenario",scenario);insert.Parameters.AddWithValue("contract",quote.ContractId);insert.Parameters.AddWithValue("epoch",quote.SourceEpoch);insert.Parameters.AddWithValue("sequence",quote.SourceSequence);insert.Parameters.AddWithValue("payload",bytes);await insert.ExecuteNonQueryAsync();}
            await using var read=new NpgsqlCommand("SELECT payload FROM broker_quote_receipts WHERE scenario=@scenario AND contract_id=@contract AND epoch=@epoch AND source_sequence=@sequence",pg);
            read.Parameters.AddWithValue("scenario",scenario);read.Parameters.AddWithValue("contract",quote.ContractId);read.Parameters.AddWithValue("epoch",quote.SourceEpoch);read.Parameters.AddWithValue("sequence",quote.SourceSequence);
            Require(((byte[])(await read.ExecuteScalarAsync())!).AsSpan().SequenceEqual(bytes),"Retry quote changed its source book.");return 0;
        };
    }
    public static async Task CrashChild(string name,string kind,string mode)
    {
        var pg=new NpgsqlConnection(Environment.GetEnvironmentVariable("IFM_BROKER_TEST_PG"));await pg.OpenAsync();
        var fixture=new BrokerOrderDeliveryFixture(){Failure=mode=="completed-no-ack"?"none":mode};Bind(fixture,pg,name);await fixture.Start();
        var connection=new NatsConnection(new NatsOpts{Url=Environment.GetEnvironmentVariable("IFM_BROKER_TEST_NATS")!});var consumer=await new NatsJSContext(connection).GetConsumerAsync(name,"ordered");
        var input=await Fetch(consumer);var source=BrokerOrderDeliveryFixture.Decode(kind,input.Data!);var id=source.Id;
        Require(await fixture.Deliver(source)==(mode=="completed-no-ack"),"Unexpected broker source settlement.");
        await foreach(var probe in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts{MaxMsgs=1,Expires=TimeSpan.FromMilliseconds(1100)},Serializer))
        {
            var next=BrokerOrderDeliveryFixture.Decode(kind,probe.Data!);
            try{Require(next.Id==id,"Successor overtook unfinished quote.");}
            finally{if(next is FuturesTickQuoteDataChangedEvent quote)quote.QuoteData.Dispose();}
        }
        Console.WriteLine("BROKER_INPUT_RETAINED");Environment.Exit(0);
    }
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {await foreach(var input in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts{MaxMsgs=1,Expires=TimeSpan.FromSeconds(5)},Serializer))return input;throw new TimeoutException("Expected source was not redelivered.");}
    static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
}
