using System.Reflection;
using System.Collections;
using System.Security.Cryptography;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Application.MarketData.Rust.Publication;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
var fixture=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement[0];
var model=MessagePackSerializer.Deserialize<FuturesOptionContractReadModel>(fixture.GetProperty("content").GetBytesFromBase64());
var count=100000;var overlap=false;var hold=false;object?[]? lastDefinitionQuery=null;var expiryReads=0;var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var securities=Proxy.Create<ISecuritiesDbContext>((method,input)=>method.Name switch{
    "GetOptionContractExpiriesAsync"=>ReadExpiries(),
    "GetCachedOptionContractDefinitionsAsync"=>Read(input),
    _=>throw new InvalidDataException("Unexpected storage operation: "+method.Name)
});
Task<IReadOnlyList<OptionContractExpiryReadModel>> ReadExpiries(){expiryReads++;return Task.FromResult<IReadOnlyList<OptionContractExpiryReadModel>>(overlap
    ?[new(){Symbol="ES",ContractId="ES20261218",ExpiryDate=new(2026,11,10)},new(){Symbol="ES",ContractId="ES20261218",ExpiryDate=new(2026,11,11)}]
    :[new(){Symbol="ES",ContractId="ES20261218",ExpiryDate=new(2026,11,10)}]);}
async Task<IReadOnlyList<CachedOptionContractDefinitionReadModel>> Read(object?[]? input){lastDefinitionQuery=input;if(hold){entered.TrySetResult();await release.Task;}return new Rows(overlap && input?[2] is DateOnly expiry && expiry.Day==11 ? model with{StrikePrice=model.StrikePrice+1}:model,count);}
var factory=Proxy.Create<IDbContextFactory>((method,input)=>method.Name=="get_SecuritiesDb"?securities:throw new InvalidDataException("Unexpected database access."));
var source=new NativeDefinitionFacts(factory);
var paging=new TomasAI.IFM.Application.MarketData.Rust.Persistence.NativeReferenceStorage(new ReferenceFacts(source));
ulong next=0;
async Task<byte[]> Transfer(JsonElement arguments,CancellationToken token){
    var id=++next;var input=JsonSerializer.SerializeToUtf8Bytes(arguments);using var bytes=new MemoryStream();string? signature=null;uint total=0;
    while(true){var page=await paging.ReadAsync(new(id,7,input,checked((uint)bytes.Length)),token);
        signature??=page.Digest;total=total==0?page.Total:total;
        if(signature!=page.Digest||total!=page.Total||page.Offset!=bytes.Length||page.Bytes.Length>4096)throw new InvalidDataException("Raw paging integrity changed.");
        bytes.Write(page.Bytes);if(bytes.Length==page.Total)break;
    }
    var content=bytes.ToArray();if(Convert.ToHexString(SHA256.HashData(content))!=signature)throw new InvalidDataException("Raw paging content changed.");return content;
}
JsonElement Query(object value)=>JsonSerializer.SerializeToElement(value);
var begin=Query(new{root="ES",from=new DateOnly(2026,10,27).DayNumber,until=new DateOnly(2026,12,27).DayNumber});
var header=JsonDocument.Parse(await Transfer(begin,default)).RootElement;
if(header.GetProperty("count").GetInt32()!=100000)throw new InvalidDataException("Complete source was truncated.");
using(var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256)){
 for(var offset=0;offset<count;offset+=256){var page=await Transfer(Query(new{snapshot=header.GetProperty("id").GetString(),offset}),default);
  using var rows=JsonDocument.Parse(page);if(rows.RootElement.GetArrayLength()!=Math.Min(256,count-offset)||rows.RootElement[0].GetArrayLength()!=24||page.Length>524288)throw new InvalidDataException("Unbounded/incorrect compact source page.");digest.AppendData(page);
 }
 if(Convert.ToHexString(digest.GetHashAndReset())!=header.GetProperty("digest").GetString())throw new InvalidDataException("Immutable full source digest mismatch.");
}
foreach(var offset in new[]{-1,1,100096}){try{await Transfer(Query(new{snapshot=header.GetProperty("id").GetString(),offset}),default);throw new Exception("Invalid page accepted.");}catch(InvalidDataException){}}
count=257;var second=JsonDocument.Parse(await Transfer(begin,default)).RootElement;
model=model with{Description="changed after capture"};
var stable=await Transfer(Query(new{snapshot=second.GetProperty("id").GetString(),offset=256}),default);
if(JsonDocument.Parse(stable).RootElement.GetArrayLength()!=1)throw new InvalidDataException("Last partial page is incomplete.");
count=0;var third=JsonDocument.Parse(await Transfer(begin,default)).RootElement;
if(third.GetProperty("count").GetInt32()!=0)throw new InvalidDataException("Empty source was not preserved.");
try{await Transfer(Query(new{snapshot=header.GetProperty("id").GetString(),offset=0}),default);throw new Exception("Retired snapshot was not evicted.");}catch(InvalidDataException){}
count=100001;try{await Transfer(begin,default);throw new Exception("Oversized source accepted.");}catch(InvalidDataException){}
count=1;hold=true;var pending=Transfer(begin,default);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
try{await Transfer(begin,default);throw new Exception("Concurrent unbounded capture accepted.");}catch(InvalidOperationException){}
release.TrySetResult();await pending;hold=false;
using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{await Transfer(begin,cancelled.Token);throw new Exception("Cancelled source accepted.");}catch(OperationCanceledException){}}
// Overlapping calendar partitions repeat canonical identities. Compare the
// complete frozen pages with a single partition; first row wins as in the
// existing C# universe dictionary, even if a later partition differs.
count=257;var uniqueHeader=JsonDocument.Parse(await Transfer(begin,default)).RootElement;
var uniquePages=new List<byte[]>();
for(var offset=0;offset<count;offset+=256)uniquePages.Add(await Transfer(Query(new{snapshot=uniqueHeader.GetProperty("id").GetString(),offset}),default));
overlap=true;
var overlapHeader=JsonDocument.Parse(await Transfer(begin,default)).RootElement;
if(overlapHeader.GetProperty("count").GetInt32()!=257||overlapHeader.GetProperty("digest").GetString()!=uniqueHeader.GetProperty("digest").GetString())throw new InvalidDataException("Overlapping calendar partitions changed canonical first-row authority.");
for(var offset=0;offset<count;offset+=256){var page=await Transfer(Query(new{snapshot=overlapHeader.GetProperty("id").GetString(),offset}),default);if(!page.SequenceEqual(uniquePages[offset/256]))throw new InvalidDataException("Overlapping partition changed immutable page bytes.");}
overlap=false;
count=257;var previousExpiryReads=expiryReads;
var exactBegin=Query(new{root="ES",underlying="ES20261218",from=new DateOnly(2026,11,10).DayNumber,until=new DateOnly(2026,11,10).DayNumber,provider_roots=new[]{"EW4","ES"},browser=true});
var exactHeader=JsonDocument.Parse(await Transfer(exactBegin,default)).RootElement;
if(expiryReads!=previousExpiryReads||lastDefinitionQuery?[1] as string!="ES20261218"
    ||lastDefinitionQuery?[3] is not IReadOnlyCollection<string> filter||!filter.SequenceEqual(new[]{"EW4","ES"}))throw new InvalidDataException("Exact browser source changed its copied filters or scanned other expiries.");
var exactPage=JsonDocument.Parse(await Transfer(Query(new{snapshot=exactHeader.GetProperty("id").GetString(),offset=0}),default)).RootElement;
if(exactPage.GetArrayLength()!=256||exactPage[0].GetArrayLength()!=25||exactPage[0][24].GetString()!=model.OptionType)throw new InvalidDataException("Browser projection lost untouched legacy option-type text.");
foreach(var invalid in new object[]{new{root="ES",from=1,until=2,browser=true},new{root="ES",underlying="future",from=1,until=2,browser=true},new{root="ES",underlying="future",from=1,until=1,provider_roots=new[]{""},browser=true}}){try{await Transfer(Query(invalid),default);throw new Exception("Invalid exact browser filter accepted.");}catch(InvalidDataException){}}
if(typeof(NativeDefinitionFacts).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Any(field=>field.FieldType.FullName?.Contains("NativeMarketDataRuntime")==true))throw new Exception("Passive source retained native owner.");
Console.WriteLine("PASS compact C# raw source through actual eight-entry/4096-byte paging wrapper: complete 100000 rows; cross-partition duplicates preserve exact first-row pages/digest; 24-field bounded pages; SHA-256 full source integrity; immutable pages; partial/empty boundaries; invalid/expired identities; oversized/concurrent/cancelled capture; exact-expiry/underlying/provider-root filter propagation without expiry scanning; untouched 25-field browser source; malformed browser filters; no native resource handle.");
public class Proxy:DispatchProxy {
 public Func<MethodInfo,object?[]?,object?> Handler=null!;
 public static T Create<T>(Func<MethodInfo,object?[]?,object?> handler)where T:class{var proxy=DispatchProxy.Create<T,Proxy>();((Proxy)(object)proxy).Handler=handler;return proxy;}
 protected override object? Invoke(MethodInfo? method,object?[]? arguments)=>Handler(method!,arguments);
}
sealed class Rows(FuturesOptionContractReadModel model,int count):IReadOnlyList<CachedOptionContractDefinitionReadModel>{
 public int Count=>count;
 public CachedOptionContractDefinitionReadModel this[int index]=>new(){Definition=model with{ContractId=$"ES20261110C{6000+index}",InstrumentId=checked((uint)(43+index))}};
 public IEnumerator<CachedOptionContractDefinitionReadModel> GetEnumerator(){for(var index=0;index<count;index++)yield return this[index];}
 IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
}

sealed class ReferenceFacts(NativeDefinitionFacts source):TomasAI.IFM.Application.MarketData.Rust.Persistence.INativeReferenceFacts {
 public async Task<byte[]> ReadAsync(byte kind,ReadOnlyMemory<byte> args,CancellationToken token){
  if(kind!=7)throw new InvalidDataException("Unexpected reference operation.");using var input=JsonDocument.Parse(args);return await source.ReadAsync(input.RootElement,token);
 }
}
