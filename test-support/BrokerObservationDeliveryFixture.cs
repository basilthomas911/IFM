using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.TestSupport;

/// <summary>Actual financial file, emulator and bridge; only the publication receiver is controlled.</summary>
public sealed class BrokerObservationDeliveryFixture : IAsyncDisposable
{
    public readonly BrokerEmulatorSettlementFixture Real;
    public readonly IActorSupervisor Supervisor = Substitute.For<IActorSupervisor>();
    public readonly IJSActorProducer Producer = Substitute.For<IJSActorProducer>();
    public Func<BrokerOrderObservationReceivedEvent, CancellationToken, ValueTask> Publish = (_, _) => ValueTask.CompletedTask;
    public readonly string OrderId;
    public BrokerObservationDeliveryFixture(string path, int legs = 1, bool seed = true)
    {
        Real = new(path);
        var component = new Guid(800,0,0,new byte[8]);
        OrderId = $"1.1.1.{new Guid(801,0,0,new byte[8]):N}.{component:N}";
        Supervisor.GetJSProducer(Arg.Any<ActorMailboxId>()).Returns(Producer);
        Producer.SendAsync<BrokerOrderObservationReceivedEvent,BrokerOrderId>(Arg.Any<ActorSubject>(),Arg.Any<BrokerOrderObservationReceivedEvent>(),Arg.Any<CancellationToken>())
            .Returns(call => Publish(call.Arg<BrokerOrderObservationReceivedEvent>(), call.Arg<CancellationToken>()));
        if (!seed) return;
        Real.Ledger.Place(new("MAILBOX-TEST",OrderId,new Guid(802,0,0,new byte[8]),component,
            legs switch {1=>FrameworkOrderShape.FuturesOutright,2=>FrameworkOrderShape.VerticalSpread,_=>FrameworkOrderShape.IronCondor},false,
            Enumerable.Range(0,legs).Select(i=>new FrameworkOrderLeg(new Guid(810+i,0,0,new byte[8]),"C"+i,i%2==0?1:-1,legs==1?null:100m+i,null,null,50m)).ToArray(),
            20m,20m,20m,.25m,Real.Time.UtcNow.AddMinutes(5),"approved","reference",1000m,1000m));
        for(var i=0;i<legs;i++) Real.Ledger.PublishQuote(new("C"+i,9m,10m,10,10,Real.Time.UtcNow,1,i+1));
    }
    public BrokerOrderObservationBridge Bridge() => new(Real.Broker,Supervisor,NullLogger<BrokerOrderObservationBridge>.Instance);
    public int ExpectedCursor
    {
        get
        {
            var facts=Real.Checkpoints.Load()!.Observations;
            return Array.FindLastIndex(facts,f=>f.Kind!=FrameworkObservationKind.AccountSnapshot)+1;
        }
    }
    public async Task WaitForCursor()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var expected=ExpectedCursor;
        while(Real.Checkpoints.Load()!.PublishedOrderObservationCount<expected)
            await Task.Delay(10,timeout.Token);
    }
    public ValueTask DisposeAsync(){Real.Checkpoints.Dispose();return ValueTask.CompletedTask;}
}
