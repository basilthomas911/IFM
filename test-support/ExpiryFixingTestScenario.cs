using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
namespace TomasAI.IFM.Tests.Support;
public static class ExpiryFixingTestScenario
{
    public static readonly DateOnly Date=new(2026,11,20);
    public static readonly DateTime Expiry=new(2026,11,20,21,0,0,DateTimeKind.Utc);
    public static readonly DateTime Boundary=Expiry.AddHours(1);
    public const string Header="ts_recv,ts_event,rtype,publisher_id,instrument_id,ts_ref,price,quantity,sequence,ts_in_delta,stat_type,channel_id,update_action,stat_flags";
    public static long Ns(DateTime time)=>(time.Ticks-DateTime.UnixEpoch.Ticks)*100;
    public static OptionPricingConvention Convention(decimal strike=7525,bool call=false)=>new()
    {
        SchemaVersion=3,ContractId=$"ES20261120{(call?'C':'P')}{strike}",Dataset="GLBX.MDP3",PublisherId=1,
        InstrumentId=checked((uint)strike),RawSymbol=$"EW3X6 {(call?'C':'P')}{strike}",Root="ES",Exchange="XCME",Currency="USD",
        UnderlyingContractId="ES20261218",ExerciseStyle=OptionExerciseStyle.European,SettlementStyle=OptionSettlementStyle.DeliveryOfFuture,
        ExpirationUtc=new(Expiry),LastTradingUtc=new(Expiry),DayCount=PricingDayCount.Actual365Fixed,CalendarVersion="test-reviewed",
        Multiplier=50,TickSize=.05m,TickRuleVersion="reviewed",DefinitionDigest=new('a',64),MappingVersion="reviewed",
        EvidenceId="test-reviewed",EffectiveFromUtc=new(Expiry.AddDays(-1)),EffectiveUntilUtc=new(Expiry),
        Strike=strike,Right=call?PricingOptionRight.Call:PricingOptionRight.Put
    };
    public static ReferenceContractVersion Reference(OptionPricingConvention c)=>new(null,new()
    {
        ContractId=c.ContractId,Symbol="ES",SecurityType="FOP",Dataset=c.Dataset,PublisherId=c.PublisherId,
        InstrumentId=c.InstrumentId,RawSymbol=c.RawSymbol,DefinitionTimestampUtc=c.EffectiveFromUtc,
        DefinitionDigest=c.DefinitionDigest,ExpirationUtc=c.ExpirationUtc,LastTradingUtc=c.LastTradingUtc,
        UnderlyingContractId=c.UnderlyingContractId,UnderlyingInstrumentId=10252,UnderlyingPublisherId=1,
        StrikePriceDecimal=c.Strike,OptionRight=c.Right==PricingOptionRight.Call?ReferenceOptionRight.Call:ReferenceOptionRight.Put,
        ExerciseStyle=ReferenceExerciseStyle.European,ReviewState=ReferenceReviewState.Reviewed,SchemaVersion=1
    },c);
    public static PositionClosingInstrument Instrument(OptionPricingConvention c)=>new(c.ContractId,c.InstrumentId,c.Dataset,true,c.RawSymbol,c.PublisherId)
    {
        ExpiryConvention=c,UnderlyingInstrumentId=10252,UnderlyingPublisherId=1,
        ReferencePayloadSha256=Convert.ToHexString(SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(Reference(c))))
    };
    public static string Row(int type=10,int action=1,int flags=0,int publisher=1,int instrument=10252,decimal fixing=6935.07m,int dateOffset=0)
        =>$"{Ns(Expiry.AddSeconds(1))+333},{Ns(Expiry.AddSeconds(1))+111},24,{publisher},{instrument},{Ns(Date.AddDays(dateOffset).ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc))},{fixing*1_000_000_000m:0},9223372036854775807,123,0,{type},0,{action},{flags}";
    public static AcquiredPositionClosingQuote Quote(OptionPricingConvention c,DateOnly? date=null)
    {
        var instrument=Instrument(c);var raw=Encoding.UTF8.GetBytes(Header+"\n"+Row()+"\n");
        var proof=new VerifiedExpiryFixing{Convention=c,ReferencePayloadSha256=instrument.ReferencePayloadSha256,
            UnderlyingInstrumentId=10252,UnderlyingPublisherId=1,ReferenceDate=Date,FixingPrice=6935.07m,
            PublishedNanoseconds=Ns(Expiry.AddSeconds(1))+111,ReceivedNanoseconds=Ns(Expiry.AddSeconds(1))+333,
            Sequence=123,CapturedAtUtc=Boundary};
        var q=new StoredPositionClosingQuote{ContractId=c.ContractId,InstrumentId=c.InstrumentId,ValueDate=date??Date,
            ExpiryFixing=proof,HasBid=false,EventTimestampNanoseconds=proof.PublishedNanoseconds,ReceiveTimestampNanoseconds=proof.ReceivedNanoseconds,
            SourceSequence=proof.Sequence,SourceArchiveSha256=Convert.ToHexString(SHA256.HashData(raw))};
        q=q with{EvidenceId=Convert.ToHexString(SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(q)))};
        return new(q,raw);
    }
    public static StrategyPositionSnapshot Position()=>new()
    {
        Id=StrategyPositionId.Create(new(101,701,6001,6001),TradeStrategyKind.IronCondor),StrategyKind=TradeStrategyKind.IronCondor,
        Phase=StrategyPositionPhase.EndOfDay,IsOpen=true,ValueDate=Date,LatestSealedValueDate=Date,PositionSequence=1,
        AsOfUtc=Boundary,CloseBoundaryUtc=Boundary,LastMarketObservationUtc=Expiry.AddDays(-1),HistoryAsOfUtc=Boundary.AddHours(-4),HistoryPositionSequence=1,
        ContractCashMultiplier=50,UnrealizedPnl=5,
        Legs=[Leg(Convention(),-1,9,5),Leg(Convention(7475),1,1,2)]
    };
    static StrategyPositionLeg Leg(OptionPricingConvention c,int quantity,decimal opening,decimal price)=>new()
    {TradeLegId=Guid.NewGuid(),ContractId=c.ContractId,AssetFamily=TradeAssetFamily.FuturesOption,SignedQuantity=quantity,OpeningPrice=opening,
        CurrentPrice=price,LastPriceAtUtc=Expiry.AddDays(-1),LastSourceSequence=17,Strike=c.Strike,Expiry=Date,CashMultiplier=50};
}
