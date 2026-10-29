using MessagePack;
using System.Text.Json;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;
var at=new DateTimeOffset(2026,10,27,12,0,0,TimeSpan.Zero).AddTicks(1);
var basis=new OptionPricingConvention {SchemaVersion=3,ContractId="ES20261110C6000",Dataset="GLBX.MDP3",PublisherId=3,InstrumentId=42,RawSymbol="fixture",Root="ES",Exchange="XCME",Currency="USD",
    UnderlyingContractId="ES20261218",ExerciseStyle=OptionExerciseStyle.European,SettlementStyle=OptionSettlementStyle.DeliveryOfFuture,ExpirationUtc=new(2026,11,10,16,0,0,TimeSpan.Zero),LastTradingUtc=new(2026,11,10,16,0,0,TimeSpan.Zero),
    DayCount=PricingDayCount.Actual365Fixed,CalendarVersion="fixture/v1",Multiplier=50,TickSize=.25m,TickRuleVersion="fixed/v1",DefinitionDigest=new('a',64),MappingVersion="fixture/v1",EvidenceId="fixture",
    EffectiveFromUtc=new(2025,1,1,0,0,0,TimeSpan.Zero),EffectiveUntilUtc=new(2027,1,1,0,0,0,TimeSpan.Zero),PremiumTickRule=OptionPremiumTickRule.Fixed,PremiumStyle=OptionPremiumStyle.PaidUpfront,UnderlyingKind=PricingUnderlyingKind.Futures,Strike=6000,Right=PricingOptionRight.Call};
var cases=new List<(string Name,OptionPricingConvention Contract,DateTimeOffset At)>();
void Add(string name,OptionPricingConvention contract,DateTimeOffset? time=null)=>cases.Add((name,contract,time??at));
Add("schema3",basis);Add("schema1",basis with{SchemaVersion=1,PremiumTickRule=OptionPremiumTickRule.Unspecified});Add("schema2",basis with{SchemaVersion=2});
Add("bands",basis with{TickSize=.05m,PremiumTickRule=OptionPremiumTickRule.CmeEsGlobex358A,TickRuleVersion=OptionPremiumTicks.CmeEsGlobexVersion});
Add("american",basis with{ExerciseStyle=OptionExerciseStyle.American});Add("other-futures-style",basis with{Root="NQ",PremiumStyle=OptionPremiumStyle.FuturesStyle,SettlementStyle=OptionSettlementStyle.Cash});
Add("actual360",basis with{DayCount=PricingDayCount.Actual360});Add("schema0",basis with{SchemaVersion=0});Add("schema4",basis with{SchemaVersion=4});
Add("fixed-zero-tick",basis with{TickSize=0});Add("fixed-negative-tick",basis with{TickSize=-.01m});Add("schema1-explicit-rule",basis with{SchemaVersion=1});
Add("schema2-unspecified-rule",basis with{SchemaVersion=2,PremiumTickRule=OptionPremiumTickRule.Unspecified});Add("bad-band-tick",basis with{PremiumTickRule=OptionPremiumTickRule.CmeEsGlobex358A});
Add("missing-id",basis with{ContractId=""});Add("missing-underlying",basis with{UnderlyingContractId=" "});Add("missing-symbol",basis with{RawSymbol="\t"});Add("missing-exchange",basis with{Exchange=null!});
Add("missing-mapping",basis with{MappingVersion=""});Add("missing-evidence",basis with{EvidenceId=""});Add("missing-calendar",basis with{CalendarVersion=""});Add("missing-tick-version",basis with{TickRuleVersion=""});
Add("missing-digest",basis with{DefinitionDigest=""});Add("nonhex-digest",basis with{DefinitionDigest=new('z',64)});Add("zero-publisher",basis with{PublisherId=0});Add("zero-instrument",basis with{InstrumentId=0});
Add("zero-multiplier",basis with{Multiplier=0});Add("negative-multiplier",basis with{Multiplier=-1});Add("before-effective",basis,basis.EffectiveFromUtc.AddTicks(-1));Add("effective-limit",basis,basis.EffectiveUntilUtc);
Add("after-last-trading",basis with{LastTradingUtc=at});Add("expired",basis with{LastTradingUtc=at,ExpirationUtc=at});Add("last-after-expiry",basis with{LastTradingUtc=basis.ExpirationUtc.AddTicks(1)});
Add("non-utc-at",basis,at.ToOffset(TimeSpan.FromHours(1)));Add("non-utc-expiry",basis with{ExpirationUtc=basis.ExpirationUtc.ToOffset(TimeSpan.FromHours(1))});
Add("unknown-exercise",basis with{ExerciseStyle=OptionExerciseStyle.Unknown});Add("unsupported-exercise",basis with{ExerciseStyle=(OptionExerciseStyle)9});Add("unknown-settlement",basis with{SettlementStyle=OptionSettlementStyle.Unknown});
Add("unknown-daycount",basis with{DayCount=PricingDayCount.Unknown});Add("unknown-right",basis with{Right=PricingOptionRight.Unknown});Add("missing-strike",basis with{Strike=null});Add("negative-strike",basis with{Strike=-1});
Add("unknown-premium",basis with{PremiumStyle=OptionPremiumStyle.Unknown});Add("es-premium-conflict",basis with{PremiumStyle=OptionPremiumStyle.FuturesStyle});
Add("unknown-underlying",basis with{UnderlyingKind=PricingUnderlyingKind.Unknown});Add("unsupported-underlying",basis with{UnderlyingKind=PricingUnderlyingKind.Equity});Add("unsupported-currency",basis with{Currency="EUR"});
Add("missing-root",basis with{Root=""});Add("missing-dataset",basis with{Dataset=""});Add("es-case-insensitive-conflict",basis with{Root="es",Exchange="cme",PremiumStyle=OptionPremiumStyle.FuturesStyle});
Add("unicode-root",basis with{Root="e\u017f",PremiumStyle=OptionPremiumStyle.FuturesStyle});
Add("schema1-american",basis with{SchemaVersion=1,PremiumTickRule=OptionPremiumTickRule.Unspecified,ExerciseStyle=OptionExerciseStyle.American});
for(var tick=0;tick<32;tick++)Add("year-fraction-"+tick,basis,at.AddTicks(tick));
var premiums=new[]{decimal.MinValue,-100.000000001m,-100m,-20.000000001m,-20m,-5.000000001m,-5m,-.000000001m,0m,5m,5.000000001m,20m,20.000000001m,100m,100.000000001m,decimal.MaxValue};
var rows=cases.Select(c=>{var failure=OptionPricingQualification.Validate(c.Contract,c.At);var ticks=new List<object>();foreach(var price in premiums)foreach(var combination in new[]{false,true}){byte[]? result=null;try{result=MessagePackSerializer.Serialize(OptionPremiumTicks.GetIncrement(c.Contract,price,combination));}catch(ArgumentException){}ticks.Add(new{price=decimal.GetBits(price),combination,result});}
return new {ticks,name=c.Name,contract=MessagePackSerializer.Serialize(c.Contract),at=c.At.UtcTicks-DateTime.UnixEpoch.Ticks,
    offset=(long)c.At.Offset.TotalMinutes,code=failure?.Code,input=failure?.Input,yearFractionBits=failure is null?(ulong?)BitConverter.DoubleToUInt64Bits(OptionPricingQualification.YearFraction(c.Contract,c.At)):null};}).ToArray();
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS exported {rows.Length} C# convention qualification and exact year-fraction fixtures.");
