using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

public static class BrokerObservationVerification
{
    static readonly NatsRawSerializer<byte[]> Serializer=NatsRawSerializer<byte[]>.Default;
    public static async Task Verify()
    {
        await using var infra=new IsolatedIntegrationInfrastructure("brokerobservations",includeCql:false);await infra.StartAsync();
        foreach(var legs in new[]{1,4})
        foreach(var storage in new[]{StreamConfigStorage.Memory,StreamConfigStorage.File})
        foreach(var mode in new[]{"before-publish","after-publish","before-cursor-save","after-cursor-save","completed"})
        {
            var name=$"OBS_{legs}_{storage}_{mode.Replace('-','_')}";var subject="brokerobservations."+name;
            var path=Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/broker-observation-crashes",Guid.NewGuid().ToString("N"),"ledger.json");
            await using var connection=new NatsConnection(new NatsOpts{Url=infra.NatsUrl});var js=new NatsJSContext(connection);
            await js.CreateStreamAsync(new StreamConfig(name,[subject]){Storage=storage,MaxMsgs=128,MaxBytes=1024*1024,Discard=StreamConfigDiscard.New});
            await js.CreateOrUpdateConsumerAsync(name,new ConsumerConfig("proof"){FilterSubject=subject,AckPolicy=ConsumerConfigAckPolicy.Explicit,MaxAckPending=1});
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add(typeof(BrokerObservationVerification).Assembly.Location);
            foreach(var arg in new[]{"--broker-observation-crash-child",name,legs.ToString(System.Globalization.CultureInfo.InvariantCulture),mode,path})start.ArgumentList.Add(arg);
            start.Environment["IFM_OBSERVATION_NATS"]=infra.NatsUrl;
            using var child=Process.Start(start)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
            try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));}catch{child.Kill(true);throw;}
            Require(child.ExitCode==0&&(await stdout).Contains("OBSERVATION_BOUNDARY"),"Child failed: "+await stderr);
            await using var recovered=new BrokerObservationDeliveryFixture(path,legs,seed:false);
            var before=recovered.Real.Checkpoints.Load()!;
            recovered.Publish=async(fact,token)=>(await js.PublishAsync(subject,JsonSerializer.SerializeToUtf8Bytes(fact),serializer:Serializer,cancellationToken:token)).EnsureSuccess();
            await using(var bridge=recovered.Bridge()){await bridge.StartAsync();await recovered.WaitForCursor();await bridge.StopAsync();}
            var after=recovered.Real.Checkpoints.Load()!;
            Require(before.Cash==after.Cash&&before.Positions.SequenceEqual(after.Positions)&&before.Observations.SequenceEqual(after.Observations),"Publication recovery changed financial facts.");
            var consumer=await js.GetConsumerAsync(name,"proof");var rows=new List<(Guid Observation,string Hash)>();
            var total=(await js.GetStreamAsync(name)).Info.State.Messages;
            for(long i=0;i<total;i++)
            {
                var item=await Fetch(consumer);using var doc=JsonDocument.Parse(item.Data!);
                var observation=doc.RootElement.GetProperty("Observation").GetProperty("ObservationId").GetGuid();
                rows.Add((observation,Convert.ToHexString(SHA256.HashData(item.Data!))));
                await item.AckAsync(new AckOpts{DoubleAck=true});
            }
            var expected=after.Observations.Where(f=>f.Kind!=FrameworkObservationKind.AccountSnapshot).Select(f=>f.ObservationId).ToArray();
            Require(rows.Select(x=>x.Observation).Distinct().SequenceEqual(expected),"Original observation order/identity changed or a fact is missing.");
            Require(rows.GroupBy(x=>x.Observation).All(g=>g.Select(x=>x.Hash).Distinct().Count()==1),"Uncertain send retry changed event content.");
            Require(rows.Count==expected.Length+(mode is "after-publish" or "before-cursor-save"?1:0),"Unexpected duplicate publication count.");
            recovered.Real.Restart();
            Require(recovered.Real.Checkpoints.Load()!.PublishedOrderObservationCount==recovered.ExpectedCursor,"Publication cursor did not survive reconstruction.");
            Console.WriteLine($"PASS {legs}-leg/{storage}/{mode}: original ordered facts retained; {rows.Count} confirmed sends / {expected.Length} unique events; financial cash/positions unchanged; durable cursor restored");
            await js.DeleteStreamAsync(name);
        }
        Console.WriteLine("20 real-emulator/bridge abrupt-child cases passed. Real JetStream confirmed byte publication; broker stayed running. Receiver is a verification consumer, not the domain financial projector; duplicate event IDs must remain idempotent downstream. PostgreSQL provisioned but not financial evidence. No live activation or power-loss qualification.");
    }
    public static async Task CrashChild(string name,int legs,string mode,string path)
    {
        var state=new BrokerObservationDeliveryFixture(path,legs);
        var connection=new NatsConnection(new NatsOpts{Url=Environment.GetEnvironmentVariable("IFM_OBSERVATION_NATS")!});var js=new NatsJSContext(connection);
        var executions=0;
        state.Publish=async(fact,token)=>
        {
            var execution=fact.Observation.Kind==BrokerOrderObservationKind.Execution;
            if(execution)
            {
                executions++;
                if(mode=="before-publish"||executions>1&&mode is "before-cursor-save" or "after-cursor-save")Crash();
            }
            (await js.PublishAsync("brokerobservations."+name,JsonSerializer.SerializeToUtf8Bytes(fact),serializer:Serializer,cancellationToken:token)).EnsureSuccess();
            if(!execution)return;
            if(mode=="after-publish")Crash();
            state.Real.Checkpoints.FailBeforeSave=mode=="before-cursor-save";
            state.Real.Checkpoints.FailAfterSave=mode=="after-cursor-save";
        };
        var bridge=state.Bridge();await bridge.StartAsync();await state.WaitForCursor();
        Require(mode=="completed","Child unexpectedly drained past fault.");Crash();
    }
    static void Crash(){Console.WriteLine("OBSERVATION_BOUNDARY");Environment.Exit(0);}
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {await foreach(var item in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts{MaxMsgs=1,Expires=TimeSpan.FromSeconds(5)},Serializer))return item;throw new TimeoutException("Missing published fact.");}
    static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
}
