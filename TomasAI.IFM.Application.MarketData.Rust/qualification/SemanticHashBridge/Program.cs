using System.Reflection;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Pricing;
var options=(JsonSerializerOptions)typeof(PricingSemanticHash).GetField("Options",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
var rows=new List<object>();
void AddDouble(double value){if(!double.IsFinite(value))return;rows.Add(new{kind="double",bits=BitConverter.DoubleToUInt64Bits(value),json=JsonSerializer.Serialize(value,options),digest=PricingSemanticHash.Compute(value)});}
void AddDecimal(decimal value){var half=(double)(value/200m);rows.Add(new{kind="decimal",bits=decimal.GetBits(value).Select(v=>(uint)v).ToArray(),json=JsonSerializer.Serialize(value,options),digest=PricingSemanticHash.Compute(value),
    halfBits=BitConverter.DoubleToUInt64Bits(half),rateBits=half>-1?(ulong?)BitConverter.DoubleToUInt64Bits(2*double.LogP1(half)):null});}
foreach(var value in new[]{0d,-0d,1d,-1d,double.Epsilon,-double.Epsilon,double.MaxValue,double.MinValue,1e-4,1e-5,1e16,1e17,1e23,Math.PI,0.1,1.2345678901234567})AddDouble(value);
for(var exponent=-324;exponent<=308;exponent++)AddDouble(Math.Pow(10,exponent));
ulong state=0x9e3779b97f4a7c15;
ulong Next(){state^=state<<13;state^=state>>7;state^=state<<17;return state;}
for(var index=0;index<4096;index++)AddDouble(BitConverter.UInt64BitsToDouble(Next()));
foreach(var value in new[]{0m,1m,-1m,.0001m,.00001m,decimal.MaxValue,decimal.MinValue,1.23000m,.0000000000000000000000000001m})AddDecimal(value);
for(byte scale=0;scale<=28;scale++) {AddDecimal(new(1,0,0,false,scale));AddDecimal(new(0,0,0,true,scale));}
for(var index=0;index<256;index++)AddDecimal(new((int)Next(),(int)Next(),(int)Next(),index%2==0,(byte)(index%29)));
foreach(var value in new[]{"", "ordinary", "\"\\\b\f\n\r\t", "+'<>&`/", "\u0000\u001f\u007f\u0085", "caf\u00e9 \u2028 \U0001f600", "\ue000 \U00010000"})
    rows.Add(new{kind="string",text=value,json=JsonSerializer.Serialize(value,options),digest=PricingSemanticHash.Compute(value)});
var objectValue=new Dictionary<string,object?>{["z"]=1,["A"]=new object?[]{true,null,"+&\"",.00001d},["\ue000"]="private",["\U00010000"]="supplementary"};
rows.Add(new{kind="object",input=JsonSerializer.SerializeToElement(objectValue),json=JsonSerializer.Serialize(objectValue,options),digest=PricingSemanticHash.Compute(objectValue)});
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS exported {rows.Count} actual C# canonical JSON/SHA-256 fixtures, including randomized finite binary64 and 96-bit decimals.");
