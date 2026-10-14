using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public class ProviderReferenceMappingTests
{
    static readonly DateTimeOffset Now = new(2026,10,13,12,0,0,TimeSpan.Zero);
    static InstrumentDefinitionSelection Definition() => new() { SnapshotId=Guid.NewGuid(),Dataset="GLBX.MDP3",PublisherId=1,InstrumentId=123,Root="ES",RawSymbol="ESZ6",InstrumentClass="F",Currency="USD",Exchange="XCME",ExpirationUtc=new(2026,12,18,14,30,0,TimeSpan.Zero),Multiplier=50m,TickSize=.25m,DefinitionDigest=new string('a',64),RawDefinitionReference="exact-reference" };
    [Fact] public void Legacy_future_upgrade_preserves_identity_and_operational_flags_but_does_not_invent_review()
    {
        var original=new FuturesContractV3ReadModel { ContractId="ES20261218",Symbol="ES",LastTradeDate=new(2026,12,18),OnTheRun=true,Rollover=true };
        var value=ProviderReferenceMapping.Future(Definition(),"America/Chicago",Now,original);
        Assert.Equal(original.ContractId,value.ContractId);Assert.True(value.OnTheRun);Assert.True(value.Rollover);
        Assert.Equal(1,value.SchemaVersion);Assert.Equal(ReferenceReviewState.Draft,value.ReviewState);Assert.Equal(50m,value.MultiplierValue);Assert.Equal("exact-reference",value.RawDefinitionReference);
        Assert.Equal(ReferenceReviewState.Draft,value.ReviewState);
        Assert.Throws<ArgumentException>(()=>ProviderReferenceMapping.Future(Definition(),"America/Chicago",Now,original with{LastTradeDate=new(2026,11,20)}));
    }
    [Fact] public void Provider_identity_and_expiry_are_checked_even_when_saving_a_draft()
    {
        var source=Definition();var original=ProviderReferenceMapping.Future(source,"America/Chicago",Now);
        Assert.Throws<ArgumentException>(()=>ProviderReferenceMapping.Future(source,"America/Chicago",Now,original with{ReviewState=ReferenceReviewState.Reviewed}));
        Assert.Throws<ArgumentException>(()=>ProviderReferenceMapping.Future(source with{InstrumentId=456},"America/Chicago",Now,original));
        Assert.Throws<ArgumentException>(()=>ProviderReferenceMapping.Future(source with{ExpirationUtc=Now.AddDays(-1)},"America/Chicago",Now));
    }
    [Fact] public void Option_mapping_retains_exact_strike_and_requires_the_exact_provider_underlying()
    {
        var underlying=ProviderReferenceMapping.Future(Definition(),"America/Chicago",Now);
        var source=Definition() with{InstrumentClass="C",InstrumentId=456,UnderlyingInstrumentId=123,Strike=6500.25m,ExpirationUtc=new(2026,11,20,21,0,0,TimeSpan.Zero)};
        var mapped=ProviderReferenceMapping.Option(source,underlying,"America/Chicago",Now);
        Assert.Equal(6500.25m,mapped.StrikePriceDecimal);Assert.Equal(underlying.ContractId,mapped.UnderlyingContractId);
        Assert.Throws<ArgumentException>(()=>ProviderReferenceMapping.Option(source,underlying with{SchemaVersion=0},"America/Chicago",Now));
        Assert.Throws<ArgumentException>(()=>ProviderReferenceMapping.Option(source,underlying with{InstrumentId=789},"America/Chicago",Now));
    }
    [Fact] public async Task Mapping_requires_current_page_identity_generation_and_explicit_preview()
    {
        int calls=0;var gateway=new ContractEditorGateway(input=>{calls++;return Task.FromResult(new ContractEditorResult(input.View,[new("cached",new())],new()));});
        ContractEditorIntent Request(string action,long generation,string? id)=>new(1,Guid.NewGuid(),"provider-definitions",action,generation,id,new());
        Assert.Equal(-5,gateway.Submit(Request("preview",1,"cached")));Assert.Equal(0,gateway.Submit(Request("load",0,null)));await gateway.DrainAsync();
        var generation=gateway.Snapshot().Generation;Assert.Equal(-5,gateway.Submit(Request("preview",generation-1,"cached")));Assert.Equal(-5,gateway.Submit(Request("preview",generation,"forged")));
        Assert.Equal(0,gateway.Submit(Request("preview",generation,"cached")));await gateway.DrainAsync();Assert.Equal(2,calls);
    }
}
