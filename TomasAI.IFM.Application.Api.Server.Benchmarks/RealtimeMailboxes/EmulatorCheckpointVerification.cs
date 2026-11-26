using System.Diagnostics;
using MessagePack;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Application.TradeBroker;
using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.OrderExecution;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.BrokerAccount;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;
public static class EmulatorCheckpointVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer=new();
    sealed class State:IDisposable
    {
        public readonly BrokerEmulatorSettlementFixture.Store Store;
        public readonly BrokerEmulatorSettlementFixture.Clock Clock=new();
        public readonly EmulatorLedger Ledger;
        public readonly TomasAI.IFM.Application.TradeBroker.Contracts.ITradeBroker Broker;
        public State(string path)
        {
            Store=new(path);Ledger=new(new("CHECKPOINT-TEST","USD",1_000_000m,.65m,TimeSpan.FromMinutes(1),1),Clock,Store);
            Broker=new InteractiveBrokersEmulatorTradeBroker(new EmulatedOrderExecutionBroker(Ledger),new EmulatedBrokerAccount(Ledger));
        }
        public void Seed(int legs)
        {
            Ledger.Place(new FrameworkOrderRequest("CHECKPOINT-TEST","order",new Guid(501,0,0,new byte[8]),new Guid(502,0,0,new byte[8]),
                legs switch{1=>FrameworkOrderShape.FuturesOutright,2=>FrameworkOrderShape.VerticalSpread,_=>FrameworkOrderShape.IronCondor},false,
                Enumerable.Range(0,legs).Select(i=>new FrameworkOrderLeg(new Guid(i+600,0,0,new byte[8]),"C"+i,(i%2==0?1:-1)*2,legs==1?null:100m+i,null,null,50m)).ToArray(),
                20m,20m,20m,.25m,Clock.UtcNow.AddMinutes(5),"approved","reference",1000m,1000m));
            for(var i=0;i<legs-1;i++)Ledger.PublishQuote(new("C"+i,9m,10m,10,10,BrokerOrderDeliveryFixture.At,new DateOnly(2026,8,25).DayNumber,i+100));
        }
        public void Dispose()=>Store.Dispose();
    }
    static FuturesTickQuoteDataChangedEvent Source(int legs,int seq)
    {
        var source=(FuturesTickQuoteDataChangedEvent)BrokerOrderDeliveryFixture.Input("Futures",seq);
        var last=source.QuoteData.Buffer[^1] with{BidPrice=9m,AskPrice=10m,BidSize=10,AskSize=10};
        return source with{TickDataId=source.TickDataId with{ContractId="C"+(legs-1)},QuoteCount=1,QuoteData=new([last],1)};
    }
    public static async Task Verify()
    {
        await using var infra=new IsolatedIntegrationInfrastructure("emulatorcheckpoint",includeCql:false);await infra.StartAsync();
        foreach(var legs in new[]{1,2,4})
        foreach(var storage in new[]{StreamConfigStorage.Memory,StreamConfigStorage.File})
        foreach(var mode in new[]{"before-save","after-save","completed-no-ack"})
        {
            var name=$"CHECKPOINT_{legs}_{storage}_{mode.Replace('-','_')}";var subject="emulatorcheckpoint."+name;
            var path=Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/emulator-checkpoint-crashes",Guid.NewGuid().ToString("N"),"ledger.json");
            await using var connection=new NatsConnection(new NatsOpts{Url=infra.NatsUrl});var js=new NatsJSContext(connection);
            await js.CreateStreamAsync(new StreamConfig(name,[subject]){Storage=storage,Retention=StreamConfigRetention.Workqueue,MaxMsgs=16,MaxBytes=1024*1024,Discard=StreamConfigDiscard.New});
            await js.CreateOrUpdateConsumerAsync(name,new ConsumerConfig("ordered"){FilterSubject=subject,AckPolicy=ConsumerConfigAckPolicy.Explicit,MaxAckPending=1,AckWait=TimeSpan.FromSeconds(3),MaxDeliver=-1});
            foreach(var seq in new[]{1,2})(await js.PublishAsync(subject,MessagePackSerializer.Serialize(Source(legs,seq)),serializer:Serializer)).EnsureSuccess();
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(typeof(EmulatorCheckpointVerification).Assembly.Location);
            foreach(var arg in new[]{"--emulator-checkpoint-crash-child",name,legs.ToString(System.Globalization.CultureInfo.InvariantCulture),mode,path})start.ArgumentList.Add(arg);
            start.Environment["IFM_CHECKPOINT_NATS"]=infra.NatsUrl;
            using var child=Process.Start(start)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
            try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));}catch{child.Kill(true);throw;}
            Require(child.ExitCode==0&&(await stdout).Contains("EMULATOR_SOURCE_RETAINED"),"Child failed: "+await stderr);
            using var recovered=new State(path);var saved=recovered.Store.Load()!;
            var initialIds=saved.Observations.Where(f=>f.Kind==FrameworkObservationKind.Execution).Select(f=>f.ObservationId).ToArray();
            Require(initialIds.Length==(mode=="before-save"?0:legs),"Financial head is partially committed.");
            await using var actor=new BrokerOrderDeliveryFixture();actor.Publish=q=>recovered.Broker.PublishMarketQuoteAsync(q);await actor.Start();
            var consumer=await js.GetConsumerAsync(name,"ordered");
            foreach(var seq in new[]{1,2})
            {
                var input=await Fetch(consumer);var source=MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(input.Data!);
                Require(source.Id==Source(legs,seq).Id,"Original source order changed.");
                Require(await actor.Deliver(source),"Actual emulator replay failed.");
                var facts=recovered.Ledger.Reconcile("order");var executions=facts.Where(f=>f.Kind==FrameworkObservationKind.Execution).ToArray();
                Require(executions.Length==legs*seq,"A source replay added or lost a partial fill.");
                Require(initialIds.All(id=>executions.Any(f=>f.ObservationId==id)),"Committed original execution IDs changed.");
                Require(recovered.Ledger.RemainingStrategyUnits("order")==2-seq,"Partial-fill progress changed.");
                await input.AckAsync(new AckOpts{DoubleAck=true});
            }
            var final=recovered.Store.Load()!;
            Require(final.Observations.Count(f=>f.Kind==FrameworkObservationKind.Execution)==legs*2,"Final executions were not saved.");
            var restored=new EmulatorLedger(new("CHECKPOINT-TEST","USD",1_000_000m,.65m,TimeSpan.FromMinutes(1),1),recovered.Clock,recovered.Store);
            Require(restored.Snapshot().CashBalance==recovered.Ledger.Snapshot().CashBalance,"Restart lost cash.");
            Require(restored.Snapshot().Positions.SequenceEqual(recovered.Ledger.Snapshot().Positions),"Restart lost positions.");
            foreach(var seq in new[]{1,2})Require(await actor.Deliver(MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(MessagePackSerializer.Serialize(Source(legs,seq)))),"Delayed duplicate failed.");
            Require(recovered.Ledger.Reconcile("order").Count(f=>f.Kind==FrameworkObservationKind.Execution)==legs*2,"Delayed duplicate consumed more liquidity.");
            await consumer.RefreshAsync();Require(consumer.Info.NumPending==0&&consumer.Info.NumAckPending==0,"Input remains pending.");
            Console.WriteLine($"PASS {legs}-leg/{storage}/{mode}: actual actor, application facade, matching engine and flushed file head; two ordered partial fills; original IDs/cash/positions survive reconstruction; no pending source");
            await js.DeleteStreamAsync(name);
        }
        Console.WriteLine("18 abrupt API-child cases use real emulator financial storage; no PostgreSQL surrogate financial receipt. Disposable infrastructure also provisions PostgreSQL but financial state is the file checkpoint. Memory/File input broker remains running; no broker/OS power-loss, live activation, legacy uncaptured-input reconciliation or downstream callback consumption guarantee.");
    }
    public static async Task CrashChild(string name,int legs,string mode,string path)
    {
        var state=new State(path);state.Seed(legs);
        state.Store.FailBeforeSave=mode=="before-save";state.Store.FailAfterSave=mode=="after-save";
        var actor=new BrokerOrderDeliveryFixture();actor.Publish=q=>state.Broker.PublishMarketQuoteAsync(q);await actor.Start();
        var connection=new NatsConnection(new NatsOpts{Url=Environment.GetEnvironmentVariable("IFM_CHECKPOINT_NATS")!});var consumer=await new NatsJSContext(connection).GetConsumerAsync(name,"ordered");
        var input=await Fetch(consumer);var source=MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(input.Data!);var sourceId=source.Id;
        Require(await actor.Deliver(source)==(mode=="completed-no-ack"),"Unexpected child financial settlement.");
        if(mode!="completed-no-ack")Require(!state.Ledger.Snapshot().Complete&&!state.Ledger.Snapshot().NewRiskAllowed,"Unresolved save appears healthy.");
        await foreach(var probe in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts{MaxMsgs=1,Expires=TimeSpan.FromMilliseconds(1100)},Serializer))
        {var next=MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(probe.Data!);try{Require(next.Id==sourceId,"A newer quote overtook the failed commit.");}finally{next.QuoteData.Dispose();}}
        Console.WriteLine("EMULATOR_SOURCE_RETAINED");Environment.Exit(0);
    }
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {await foreach(var item in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts{MaxMsgs=1,Expires=TimeSpan.FromSeconds(5)},Serializer))return item;throw new TimeoutException("Expected retained source.");}
    static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
}
