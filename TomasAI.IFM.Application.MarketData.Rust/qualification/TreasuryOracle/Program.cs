using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
var at=new DateTimeOffset(2026,9,8,16,0,0,TimeSpan.Zero);
var fields=new[]{"0","-0.00","+0.00","5.000","+.5","-.5","1.",".5","001.2300","-199.99999999999999999999999999","-200","1000","1000.0000000000000000000000001",
    "1000.00000000000000000000000001","1000.000000000000000000000000001","0.00000000000000000000000000005","0.00000000000000000000000000015",
    "0.00000000000000000000000000025","5.12345678901234567890123456785","5.12345678901234567890123456795"," 5","5 ","5e0","5_0","NaN","Infinity",".","+",""," ","5&#x2E;25","5<![CDATA[.25]]>","5<x/>.25","+0000000000000000000000000000000005.0000","-0.00000000000000000000000000005"};
string Entry(string field,string date="2026-09-08T00:00:00")=>$"<entry><content><m:properties><d:NEW_DATE>{date}</d:NEW_DATE><d:BC_1MONTH>{field}</d:BC_1MONTH></m:properties></content></entry>";
string Feed(string body)=>"<feed xmlns='http://www.w3.org/2005/Atom' xmlns:d='http://schemas.microsoft.com/ado/2007/08/dataservices' xmlns:m='http://schemas.microsoft.com/ado/2007/08/dataservices/metadata'>"+body+"</feed>";
var cases=fields.Select((value,i)=>(Name:$"decimal-{i}",Xml:Feed(Entry(value)))).ToList();
var random=new Random(64231);
for(var test=0;test<250;test++){
    var digits=new string(Enumerable.Range(0,random.Next(0,61)).Select(_=>(char)('0'+random.Next(10))).ToArray());
    var number=(random.Next(2)==0?"-":"+")+new string('0',random.Next(0,35))+random.Next(0,1100).ToString(CultureInfo.InvariantCulture)+"."+digits;
    cases.Add(($"generated-decimal-{test}",Feed(Entry(number))));
}

cases.AddRange(new[]{("empty",Feed("")),("null",Feed(Entry("5").Replace("<d:BC_1MONTH>","<d:BC_1MONTH m:null='true'>"))),
    ("duplicate-equal-scale",Feed(Entry("5.0")+Entry("5.00"))),("duplicate-conflict",Feed(Entry("5")+Entry("6"))),
    ("missing-date",Feed(Entry("5").Replace("<d:NEW_DATE>2026-09-08T00:00:00</d:NEW_DATE>",""))),
    ("duplicate-field",Feed(Entry("5").Replace("</m:properties>","<d:BC_1MONTH>5</d:BC_1MONTH></m:properties>"))),
    ("wrong-namespace",Feed(Entry("5")).Replace("http://schemas.microsoft.com/ado/2007/08/dataservices'","urn:wrong'")),
    ("pagination",Feed("<link rel='next'/>"+Entry("5"))),("empty-dtd","<!DOCTYPE feed>"+Feed(Entry("5"))),
    ("entity-dtd","<!DOCTYPE feed [<!ENTITY value '5'>]>"+Feed(Entry("&value;"))),
    ("31-entries",Feed(string.Concat(Enumerable.Repeat(Entry("5"),31)))),("32-entries",Feed(string.Concat(Enumerable.Repeat(Entry("5"),32)))),
    ("all-tenors",Feed(Entry("5").Replace("</m:properties>","<d:BC_2MONTH>5.25</d:BC_2MONTH><d:BC_3MONTH>5.50</d:BC_3MONTH><d:BC_6MONTH>5.75</d:BC_6MONTH><d:BC_1YEAR>6</d:BC_1YEAR><d:BC_2YEAR>6.1</d:BC_2YEAR><d:BC_3YEAR>6.2</d:BC_3YEAR><d:BC_5YEAR>6.3</d:BC_5YEAR><d:BC_7YEAR>6.4</d:BC_7YEAR><d:BC_10YEAR>6.5</d:BC_10YEAR><d:BC_20YEAR>6.6</d:BC_20YEAR><d:BC_30YEAR>6.7</d:BC_30YEAR></m:properties>")))});
foreach(var date in new[]{"2026-09-08T00:00:01","2026-09-08T00:00:00Z","2026-09-31T00:00:00","2026-10-01T00:00:00","2025-09-08T00:00:00","2026-09-?T00:00:00","2026-09-08T?0:00"})cases.Add(("date-"+date,Feed(Entry("5",date))));
var truncated=Feed(Entry("5.0"));for(var i=0;i<truncated.Length;i++)cases.Add(($"truncated-{i}",truncated[..i]));
var output=new List<object>();
foreach(var (name,xml) in cases){
    using var http=new HttpClient(new Source(xml));using var source=new UsTreasuryCurve(http,new FrozenTime(at));
    try{var result=await source.GetRangeAsync(new(2026,9,1),new(2026,9,30));
        output.Add(new{name,xml,valid=true,rows=result.Select(row=>new{date=row.ValueDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
            retrieved=row.RetrievedAtUtc.UtcTicks-DateTimeOffset.UnixEpoch.Ticks,rates=row.Rates.Select(point=>new{months=(int)point.Tenor,bits=decimal.GetBits(point.RatePercent).Select(x=>unchecked((uint)x)).ToArray()})}).ToArray()});
    }catch(Exception error)when(error is InvalidDataException or System.Xml.XmlException){output.Add(new{name,xml,valid=false});}
}
var serialized=JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true});
if(args.Length>1&&await File.ReadAllTextAsync(args[1])!=serialized)throw new InvalidDataException("Current C# Treasury output differs from its frozen parser oracle.");
await File.WriteAllTextAsync(args[0],serialized);
Console.WriteLine($"Recorded {output.Count} unchanged C# Treasury XML results.");
sealed class FrozenTime(DateTimeOffset at):TimeProvider{public override DateTimeOffset GetUtcNow()=>at;}
sealed class Source(string xml):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(xml,Encoding.UTF8,"application/xml")});}
