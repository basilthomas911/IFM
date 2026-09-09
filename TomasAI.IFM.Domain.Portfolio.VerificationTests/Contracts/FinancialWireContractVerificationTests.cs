using System.Reflection;
using FluentAssertions;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Contracts;

[Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-01")]
public sealed class FinancialWireContractVerificationTests
{
    public static TheoryData<Type,int> Contracts=>new()
    {
        {typeof(PostFundTransactionCommand),17},{typeof(PostFundTransactionsCommand),17},
        {typeof(ConfigureLedgerCommand),17},{typeof(LedgerConfigurationCompletedEvent),16},
        {typeof(SubmitEmulatorOrderCommand),17},{typeof(EmulatorOrderSubmittedEvent),16},
        {typeof(ReservePortfolioTradeRiskCommand),17},{typeof(ConsumeCapacityReservationCommand),17},{typeof(ChangeCapacityReservationCommand),17},
        {typeof(LedgerPostingCompletedEvent),16},{typeof(LedgerPostingBatchCompletedEvent),16},
        {typeof(CapacityReservationCompletedEvent),16},{typeof(CapacityConsumptionCompletedEvent),16},{typeof(CapacityLifecycleCompletedEvent),16},
        {typeof(LedgerPostingFailedEvent),25},{typeof(LedgerPostingBatchFailedEvent),25},
        {typeof(CapacityReservationFailedEvent),25},{typeof(CapacityConsumptionFailedEvent),25},{typeof(CapacityLifecycleFailedEvent),25}
    };

    [Theory,MemberData(nameof(Contracts))]
    public void Financial_messages_have_frozen_contiguous_numeric_keys_and_use_standard_transport(Type contract,int expectedKeys)
    {
        var properties=contract.GetProperties().Select(x=>(Property:x,Key:x.GetCustomAttribute<KeyAttribute>())).Where(x=>x.Key is not null).ToArray();
        properties.Select(x=>x.Key!.IntKey!.Value).Order().Should().Equal(Enumerable.Range(0,expectedKeys));
        properties.Single(x=>x.Key!.IntKey==0).Property.Name.Should().Be("SchemaVersion");
        var message=Activator.CreateInstance(contract)!;
        typeof(FinancialWireContractVerificationTests).GetMethod(nameof(RoundTrip),BindingFlags.Static|BindingFlags.NonPublic)!
            .MakeGenericMethod(contract).Invoke(null,[message]);
    }

    [Fact]
    public void Nested_decimals_dates_references_and_failure_disposition_survive_standard_transport()
    {
        var now=new DateTime(2026,9,8,12,34,56,DateTimeKind.Utc).AddTicks(1234567);
        var request=new PostFundTransactionCommand { OperationId=Guid.NewGuid(),Body=new() {
            Amount=123456789.12m,AccountingDate=new(2026,9,8),ValueDate=new(2026,9,9),SettlementDate=new(2026,9,10),
            Lines=[new() { Amount=99.99m,PostingSide=PostingSide.Credit }],Source=new() { OccurredAtUtc=now,SourceEventId=Guid.NewGuid() } } };
        RoundTrip(request);
        RoundTrip(new CapacityConsumptionFailedEvent { CommitDisposition=FinancialCommitDisposition.OutcomeUnknown,FailedAtUtc=now,ExistingOperationId=Guid.NewGuid() });
        RoundTrip(new ConfigureLedgerCommand { Body=new() { Action=LedgerConfigurationAction.OpenPeriod,PeriodStart=new(2026,9,1),PeriodEnd=new(2026,9,30) } });
        RoundTrip(new FinancialQuery<GetPostingReceiptRequest,FinancialOperationOutcome> { Parameters=new(Guid.NewGuid()),
            Scope=new() { PortfolioId=1101,Access=new("reader",["LedgerRead"],[1101]) } });
        RoundTrip(FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>.Complete(new()
        { Receipt=new() { Requirements=new() { SettlementCash=1.23m,MarginFunding=700m,Exposures=[new() { Amount=-0.125m,Measure=CapacityMeasure.Delta }] } } }));
    }

    [Fact]
    public void Admission_snapshot_and_internal_export_payload_use_the_same_standard_transport()
    {
        RoundTrip(new FinancialRead<FinancialAdmissionSnapshot>(FinancialReadStatus.Found,
            new(1,2,3,"Active",true,true,new(),123.45m,[],[],"Emulator","account"),7,DateTime.UtcNow));
        var payload=new AccountingExportPayload(Guid.NewGuid(),2,1,"internal-verification",7,1,new('A',64),
            [new(11,new('B',64),new(2026,9,8),10,[new(1,"cash",3,12.34m,0),new(2,"equity",3,0,12.34m)])]);
        RoundTrip(new AccountingExportReceipt(payload,FinancialCanonicalHash.Compute(payload),new('C',64),"Pending",2,null));
    }

    [Fact]
    public void Financial_command_notifications_pin_the_actual_projector_names()
    {
        ((IRequireDurableProjection)new LedgerPostingCompletedEvent()).RequiredProjection.ActorName.Should().Be("GeneralLedgerCommandActor");
        ((IRequireDurableProjection)new LedgerPostingBatchCompletedEvent()).RequiredProjection.ProjectorName.Should().Be("GeneralLedgerProjector");
        ((IRequireDurableProjection)new CapacityLifecycleCompletedEvent()).RequiredProjection.ActorName.Should().Be("CapacityReservationCommandActor");
        ((IRequireDurableProjection)new EmulatorOrderSubmittedEvent()).RequiredProjection.ProjectorName.Should().Be("EmulatorExecutionProjector");
    }

    [Fact]
    public void Financial_authorization_execution_and_posting_choices_round_trip_without_nested_serialization()
    {
        var now=DateTime.UtcNow;
        RoundTrip(new FinancialRead<FinancialPostingConfiguration>(FinancialReadStatus.Found,
            new(1,2,new(),[new(Guid.NewGuid(),3,new('A',64),LedgerTransactionKind.Commission,new(101,1),new(102,1),false)],true,DateOnly.FromDateTime(now)),4,now));
        RoundTrip(new FundRiskAuthorizationEvidence(Guid.NewGuid(),Guid.NewGuid(),new() { OrderId=3,PortfolioId=1,FundId=2,StrategyUnits=5,
            ExecutionEnvironment="Emulator",UnitCandidateHash=new('A',64),CompositionResultHash=new('B',64),SizedOrderHash=new('C',64) }));
        RoundTrip(new SubmitEmulatorOrderCommand { Body=new(new() { ExecutionId=Guid.NewGuid(),PortfolioId=1,FundId=2,BookId=3,StrategyUnits=5,
            SignedDebitPerUnit=-1.25m,EntryFees=2.50m,Legs=[new(7,"instrument","symbol","FuturesOption","Sell",5,50)],ValidUntilUtc=now },Guid.NewGuid(),Guid.NewGuid(),new('D',64)) });
        RoundTrip(new CapacityLifecycleCompletedEvent { Receipt=new() { FilledUnits=10,ClosedUnits=4,CancelledUnits=0,RemainingUnits=0 } });
    }

    [Fact]
    public void Authority_preparation_and_appended_per_trade_budget_keys_are_frozen()
    {
        RoundTrip(new FinancialRead<FinancialAuthorityDraft>(FinancialReadStatus.Found,
            new(new() { Action=LedgerConfigurationAction.RefreshAuthority,BookId=1 },["review"]),2,DateTime.UtcNow));
        RoundTrip(new PrepareFinancialAuthorityRequest(true));
        RoundTrip(new FinancialDeploymentAuthority(new(),[],123.45m));
        RoundTrip(new FinancialRead<FinancialAdmissionSnapshot>(FinancialReadStatus.Found,
            new(1,2,3,"Active",true,true,new(),1000,[],[],"Emulator","account",123.45m),4,DateTime.UtcNow));
        foreach(var (type,count) in new[] { (typeof(FinancialDeploymentAuthority),3),(typeof(FinancialAdmissionSnapshot),13),
            (typeof(PrepareFinancialAuthorityRequest),1),(typeof(FinancialAuthorityDraft),2),(typeof(FinancialPostingConfiguration),7) })
            type.GetProperties().Select(x=>x.GetCustomAttribute<KeyAttribute>()).OfType<KeyAttribute>().Select(x=>x.IntKey!.Value).Order()
                .Should().Equal(Enumerable.Range(0,count));
    }

    static void RoundTrip<T>(T value)
    {
        var bytes=MessagePackBinarySerializer.Shared.Serialize(value)!;
        var restored=MessagePackBinarySerializer.Shared.Deserialize<T>(bytes)!;
        // MessagePack timestamps deserialize as UTC. Empty DTO defaults use Unspecified MinValue;
        // compare timestamp values in the contract's UTC zone, preserving all ticks and numeric fields.
        var settings=new JsonSerializerSettings { DateTimeZoneHandling=DateTimeZoneHandling.Utc };
        JsonConvert.SerializeObject(restored,settings).Should().Be(JsonConvert.SerializeObject(value,settings));
    }

    [Fact]
    public void Ledger_controls_use_standard_typed_transport_with_exact_versions_and_reconciliation()
    {
        ((int)LedgerConfigurationAction.QualifyDevelopmentBook).Should().Be(11);
        var value=new FinancialLedgerConfiguration(7,"USD","Emulator","Active","source:12",
            [new(Guid.NewGuid(),new(2026,1,1),new(2026,12,31),3,"Closed")],
            [new(new(101,2,"Cash",PostingSide.Debit,true,new('A',64)),"Active")],
            [new(new(Guid.NewGuid(),4,new('B',64),LedgerTransactionKind.Commission,new(102,1),new(101,2),true),"Active",new(2026,1,1),null)],
            new(Guid.NewGuid(),12,5,10,125.50m,125.50m,[],"source:12",new('C',64)));
        RoundTrip(new FinancialRead<FinancialLedgerConfiguration>(FinancialReadStatus.Found,value,13,DateTime.UtcNow));
        RoundTrip(new FinancialQuery<GetFinancialLedgerConfigurationRequest,FinancialLedgerConfiguration>
            { Parameters=new(),Scope=new() { PortfolioId=1,Access=new("reader",["LedgerRead"],[1]) } });
        typeof(FinancialLedgerConfiguration).GetProperties().Select(x=>x.GetCustomAttribute<KeyAttribute>()).OfType<KeyAttribute>()
            .Select(x=>x.IntKey!.Value).Order().Should().Equal(Enumerable.Range(0,10));
    }

    [Fact]
    public void Book_preparation_preserves_generated_configuration_in_standard_transport()
    {
        RoundTrip(new FinancialPostingConfiguration(1,2,new(),[],true,new(2026,9,8),true));
        RoundTrip(new FinancialQuery<PrepareFinancialBookRequest,FinancialBookSetup> { Parameters=new("DEV",new(2026,1,1),new(2026,12,31)),
            Scope=new() { PortfolioId=1,Access=new("operator",["LedgerConfigure"],[1]) } });
        RoundTrip(new FinancialBookSetup(["DEV"],["Development Fund"],new() { Action=LedgerConfigurationAction.CreateBook,BookId=7,
            Book=new() { BookId=7,PortfolioId=1,AccountingEntityId=Guid.NewGuid(),Environment="Emulator",ExecutionAccountReference="DEV",Funds=[new() { FundId=2 }] },
            Accounts=[new(11,1,"Cash",PostingSide.Debit,true,new('A',64))],PeriodId=Guid.NewGuid(),PeriodStart=new(2026,1,1),PeriodEnd=new(2026,12,31) }));
        typeof(FinancialBookSetup).GetProperties().Select(x=>x.GetCustomAttribute<KeyAttribute>()).OfType<KeyAttribute>()
            .Select(x=>x.IntKey!.Value).Order().Should().Equal(0,1,2);
    }
}
