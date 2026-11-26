using TomasAI.IFM.Application.TradeBroker;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.BrokerAccount;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.OrderExecution;
namespace TomasAI.IFM.TestSupport;
/// <summary>Real application facade and ledger; store fault injection is before an actual file checkpoint save.</summary>
public sealed class BrokerEmulatorSettlementFixture
{
    public sealed class Clock:IEmulatorClock {public DateTime UtcNow{get;set;}=BrokerOrderDeliveryFixture.At.AddSeconds(10);}
    public sealed class Store(string path):IEmulatorLedgerStore, IDisposable
    {
        readonly FileEmulatorLedgerStore inner=new(path);
        public bool FailBeforeSave;public bool FailAfterSave;public bool FailLoad;public int Writes;
        public EmulatorLedgerCheckpoint? Load()=>FailLoad?throw new IOException("Injected unavailable checkpoint read."):inner.Load();
        public void Save(EmulatorLedgerCheckpoint checkpoint)
        {Writes++;if(FailBeforeSave)throw new IOException("Injected emulator checkpoint failure before save.");inner.Save(checkpoint);if(FailAfterSave)throw new IOException("Injected lost checkpoint-save reply.");}
        public void Dispose()=>inner.Dispose();
    }
    public readonly Store Checkpoints;
    public readonly Clock Time=new();
    public readonly EmulatorScenario Scenario=new("MAILBOX-TEST","USD",1_000_000m,.65m,TimeSpan.FromMinutes(1));
    public EmulatorLedger Ledger{get;private set;}
    public ITradeBroker Broker{get;private set;}
    public BrokerEmulatorSettlementFixture(string path)
    {Checkpoints=new(path);Ledger=new(Scenario,Time,Checkpoints);Broker=Bind(Ledger);}
    static ITradeBroker Bind(EmulatorLedger ledger)=>new InteractiveBrokersEmulatorTradeBroker(new EmulatedOrderExecutionBroker(ledger),new EmulatedBrokerAccount(ledger));
    public void Restart(){Ledger=new(Scenario,Time,Checkpoints);Broker=Bind(Ledger);}
    public ValueTask<BrokerDispatchReceipt> Place()=>Broker.PlaceAsync(new BrokerOrderRequest("MAILBOX-TEST",BrokerEnvironment.Emulator,"mailbox-order",new Guid(501,0,0,new byte[8]),new Guid(502,0,0,new byte[8]),BrokerOrderShape.FuturesOutright,false,
        [new BrokerOrderLeg(new Guid(503,0,0,new byte[8]),"ESZ6",1,null,null,null,50m)],6000m,6000m,6000m,.25m,Time.UtcNow.AddMinutes(5),"approved","reference",5000m,5000m));
    public static async Task<SaveFailureEvidence> AuditSaveFailure(string path)
    {
        var real=new BrokerEmulatorSettlementFixture(path);await real.Place();
        var before=(await real.Broker.GetAccountSnapshotAsync()).CashBalance;
        await using var actor=new BrokerOrderDeliveryFixture();actor.Publish=quote=>real.Broker.PublishMarketQuoteAsync(quote);await actor.Start();
        real.Checkpoints.FailBeforeSave=true;
        var first=await actor.Deliver(BrokerOrderDeliveryFixture.Decode("Futures",BrokerOrderDeliveryFixture.Encode(BrokerOrderDeliveryFixture.Input("Futures",1))));
        var memory=(await real.Broker.GetAccountSnapshotAsync()).CashBalance;
        var blocked=false;try{_ = await real.Broker.ReconcileOrderAsync("mailbox-order");}catch(IOException){blocked=true;}
        var memoryFills=0;
        real.Checkpoints.FailBeforeSave=false;var writes=real.Checkpoints.Writes;
        var retry=await actor.Deliver(BrokerOrderDeliveryFixture.Decode("Futures",BrokerOrderDeliveryFixture.Encode(BrokerOrderDeliveryFixture.Input("Futures",1))));
        var additionalWrites=real.Checkpoints.Writes-writes;
        real.Restart();var restored=(await real.Broker.GetAccountSnapshotAsync()).CashBalance;
        var durableFills=(await real.Broker.ReconcileOrderAsync("mailbox-order")).Count(f=>f.Kind==BrokerObservationKind.Execution);
        real.Checkpoints.Dispose();
        return new(first,retry,before,memory,restored,memoryFills,durableFills,additionalWrites,blocked);
    }
}
public sealed record SaveFailureEvidence(bool FirstSettled,bool RetrySettled,decimal StartingCash,decimal MemoryCash,decimal RestoredCash,int MemoryExecutions,int RestoredExecutions,int RetryCheckpointWrites,bool PendingReadsBlocked = false);
