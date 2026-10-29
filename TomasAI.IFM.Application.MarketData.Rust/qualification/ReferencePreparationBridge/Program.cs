using MessagePack;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Shared.MarketData.Contracts;
var at=new DateTimeOffset(2026,10,27,12,0,0,TimeSpan.Zero).AddTicks(123);
var points=new[]{new TreasuryRatePoint(TreasuryTenor.OneMonth,4.0000m),new TreasuryRatePoint(TreasuryTenor.TwoMonth,4.10m),new TreasuryRatePoint(TreasuryTenor.ThreeMonth,4.2m)};
var basis=new TreasuryCurveSnapshot(new(2026,10,26),points,at.AddMinutes(-1),UsTreasuryCurve.Source);
var policy=UsTreasuryCurve.ConversionPolicy;
var cases=new List<(string Name,TreasuryCurveSnapshot Curve,TreasuryTenor Tenor,TreasuryRateConversionPolicy Policy)>();
void Add(string name,TreasuryCurveSnapshot? curve=null,TreasuryTenor tenor=TreasuryTenor.OneMonth,TreasuryRateConversionPolicy? conversion=null)=>cases.Add((name,curve??basis,tenor,conversion??policy));
Add("one-month");Add("two-month",tenor:TreasuryTenor.TwoMonth);Add("three-month",tenor:TreasuryTenor.ThreeMonth);
Add("wrong-source",basis with{Source="Other"});Add("wrong-country",basis with{CountryCode="CA"});Add("wrong-currency",basis with{CurrencyCode="CAD"});
Add("missing-date",basis with{ValueDate=default});Add("non-utc",basis with{RetrievedAtUtc=basis.RetrievedAtUtc.ToOffset(TimeSpan.FromHours(1))});
Add("no-rates",basis with{Rates=[]});Add("duplicate-rate",basis with{Rates=[points[0],points[0]]});Add("unknown-tenor",basis with{Rates=[new((TreasuryTenor)4,4m)]});
Add("unsupported-tenor",tenor:TreasuryTenor.SixMonth);Add("missing-tenor",basis with{Rates=[points[1]]});Add("wrong-policy",conversion:policy with{Version="other/v1"});
foreach(var amount in new[]{-200m,-200.00000000000000000000000001m,-199.99999999999999999999999999m,0m,.0000000000000000000000000001m,decimal.MaxValue,4.1234567890123456789012345678m})
    Add("rate-"+amount.ToString(System.Globalization.CultureInfo.InvariantCulture),basis with{Rates=[new(TreasuryTenor.OneMonth,amount)]});
using var client=new HttpClient();using var source=new UsTreasuryCurve(client);
var rows=cases.Select(c=>{var result=source.GetContinuouslyCompoundedAnnualRate(c.Curve,c.Tenor,c.Policy);return new{name=c.Name,day=c.Curve.ValueDate.DayNumber,
    c.Curve.Source,c.Curve.CountryCode,c.Curve.CurrencyCode,retrieved=c.Curve.RetrievedAtUtc.UtcTicks-DateTime.UnixEpoch.Ticks,offset=(int)c.Curve.RetrievedAtUtc.Offset.TotalMinutes,
    rates=c.Curve.Rates.Select(p=>new{tenor=(int)p.Tenor,words=decimal.GetBits(p.RatePercent).Select(x=>(uint)x).ToArray()}),tenor=(int)c.Tenor,
    policy=c.Policy,error=result.Error,curveDigest=TreasuryRateConversion.Digest(c.Curve),value=result.Value is null?null:MessagePackSerializer.Serialize(result.Value),
    semanticDigest=result.Value is null?null:PricingSemanticHash.Compute(result.Value)};}).ToArray();
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS exported {rows.Length} existing C# full-reference admission, curve digest, rate DTO and semantic digest fixtures.");
