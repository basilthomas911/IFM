using System.Text.Json;
using System.Text.Json.Nodes;
using TomasAI.IFM.Shared.MarketData.Definitions;

// Execute the unchanged parser, including malformed input and provider sentinels.
var cases = new List<object>();
var seed = """{"hd":{"rtype":19,"publisher_id":1,"instrument_id":42,"ts_event":"1234567890123456789"},"raw_symbol":"ESZ6","asset":"ES","instrument_class":"F","underlying_id":0,"ts_recv":"1234567890123456790","expiration":"1798191000000000000","activation":"0","strike_price":"9223372036854775807","min_price_increment":"250000000","min_price_increment_amount":"12500000000","contract_multiplier":50,"maturity_week":255,"currency":"USD","exchange":"XCME"}""";
void Add(string json) {
    try {
        var d = ExactInstrumentDefinition.Parse("GLBX.MDP3", json); var s=d.Summary;
        cases.Add(new {json,valid=true,publisher=d.PublisherId,instrument=d.InstrumentId,symbol=d.RawSymbol,asset=d.Asset,className=d.InstrumentClass,currency=d.Currency,exchange=d.Exchange,settlement=s.SettlementCurrency,security=s.SecurityType,cfi=s.Cfi,unit=s.UnitOfMeasure,underlyingSymbol=s.Underlying,kind=(int)s.ContractKind,
            received=d.ReceivedNanoseconds,at=d.EventNanoseconds,deleted=d.Deleted,underlying=s.UnderlyingInstrumentId,
            activation=s.ActivationTimestampNanoseconds,expiration=s.ExpirationTimestampNanoseconds,maturity=s.MaturityDate?.ToString("yyyy-MM-dd"),
            strike=s.StrikePrice,tick=s.MinimumPriceIncrement,tickAmount=s.MinimumPriceIncrementAmount,multiplier=s.ContractMultiplier,week=s.MaturityWeek});
    } catch(Exception) { cases.Add(new {json,valid=false}); }
}
Add(seed);
foreach(var field in new[]{"expiration","activation","strike_price","min_price_increment","min_price_increment_amount","contract_multiplier","maturity_week","underlying_id","ts_recv"})
foreach(var value in new[]{"0","-1","1","255","256","2147483647","9223372036854775807","18446744073709551615","18446744073709551616"," +42 ","1.5","", "not-a-number"}) {
    var node=JsonNode.Parse(seed)!;node[field]=value;Add(node.ToJsonString());
}
foreach(var kind in new[]{"C","P","F","X","","c"}) {var n=JsonNode.Parse(seed)!;n["instrument_class"]=kind;Add(n.ToJsonString());}
foreach(var year in new[]{0,1,2024,2026,9999,10000}) foreach(var month in new[]{0,2,12,13}) foreach(var day in new[]{0,28,29,31,32}) {
    var n=JsonNode.Parse(seed)!;n["expiration"]="18446744073709551615";n["maturity_year"]=year;n["maturity_month"]=month;n["maturity_day"]=day;Add(n.ToJsonString());
}
foreach(var field in new[]{"hd","raw_symbol","underlying_id","ts_recv"}){var n=JsonNode.Parse(seed)!;n.AsObject().Remove(field);Add(n.ToJsonString());}
Add("{}");Add("null");Add("[]");Add("{");
foreach(var field in new[]{"raw_symbol","asset","instrument_class","currency","exchange","settl_currency","security_type","cfi","unit_of_measure","underlying"})
foreach(var raw in new[]{"true","false","null","1.00","1e0","1e-05","1e1000","1e-1000",new string('9',100),"[]","{}","[1,  2]","{ \"z\": 1, \"a\": 2 }"}){
    var n=JsonNode.Parse(seed)!;n[field]=JsonNode.Parse(raw);Add(n.ToJsonString());
}
foreach(var value in new[]{"-0","-00","42\0","42\0\0","\u00a042\u00a0","42\u2003","+42","42 ","\t42\r\n"})
foreach(var field in new[]{"underlying_id","ts_recv","expiration","maturity_week","contract_multiplier","strike_price"}){
    var n=JsonNode.Parse(seed)!;n[field]=value;Add(n.ToJsonString());
}
Add(seed.Replace("\"raw_symbol\":\"ESZ6\"","\"raw_symbol\": { \"z\": 1, \"a\": [1, 2] }"));
Add(seed.Replace("\"ts_event\":\"1234567890123456789\"","\"ts_event\":-0"));
foreach(var depth in new[]{62,63,64,65,128}) Add(seed.Replace("\"raw_symbol\":\"ESZ6\"","\"raw_symbol\":"+new string('[',depth)+"1"+new string(']',depth)));
Add(seed.Replace("\"raw_symbol\":\"ESZ6\"","\"raw_symbol\": { \"$serde_json::private::Number\": \"not-a-number\" }"));
File.WriteAllText(args[0],JsonSerializer.Serialize(cases));Console.WriteLine($"Unchanged C# definition parser: {cases.Count} cases");
