using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
#if OLD_HOST
using TomasAI.IFM.Application.MarketData.Host;
#else
using TomasAI.IFM.Application.MarketData.Rust.Host;
#endif

var date=new DateOnly(2026,10,27);var eventAt=new DateTimeOffset(2026,10,20,12,0,0,TimeSpan.Zero);
var vx=new DatasetSubscriptionContract{DomainContractId="VX20261118",ProviderContractName="VXX6",Dataset="XCBF.PITCH",RootSymbol="VX",AssetTypeId=AssetTypeId.Futures,OnTheRun=true};
var results=new List<object>();
foreach(var mode in new[]{"stored","stored-invalid","history","empty","expensive","invalid-action","invalid-price","invalid-id","limit","cancel"})
{
    var db=Substitute.For<IDbContextFactory>();var raw=Substitute.For<ITradeTickQuoteDbContext>();db.TradeTickQuoteDb.Returns(raw);
    IReadOnlyList<BootstrapTradeRow> rows=mode=="stored"?[new(date,20.5m,2,77,eventAt.ToUnixTimeMilliseconds()*1000000,eventAt.ToUnixTimeMilliseconds()*1000000+100,84,0,42,1)]
        :mode=="stored-invalid"?[new(date,-2,2,80,eventAt.AddMinutes(2).ToUnixTimeMilliseconds()*1000000,1,84,0,42,1),new(date,20.5m,2,77,eventAt.ToUnixTimeMilliseconds()*1000000,eventAt.ToUnixTimeMilliseconds()*1000000+100,84,0,42,1)]:[];
    raw.GetLatestRawFuturesTradesAsync(vx.DomainContractId,date,Arg.Any<CancellationToken>()).Returns(Task.FromResult(rows));
    var historical=new History(mode,eventAt);object loader;
#if OLD_HOST
    loader=new VxBootstrapPriceService(new DatasetWorkerCurrentValues(),new DatasetDesiredSubscriptionRegistry(),db,historical,NullLogger<VxBootstrapPriceService>.Instance);
#else
    loader=new NativeVxBootstrapLoader(db,historical,TimeProvider.System,NullLogger<NativeVxBootstrapLoader>.Instance);
#endif
    using var token=new CancellationTokenSource();if(mode=="cancel")token.Cancel();
    object? value=null;string? error=null;
    try{var task=(Task<(FuturesMarketPriceSnapshot Price,string Source)?>)loader.GetType().GetMethod("LoadAsync",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(loader,[vx,date,token.Token])!;
        var result=await task;value=result is{} item?new{item.Price,item.Source}:null;}
    catch(Exception failure){error=failure.GetType().FullName;}
    results.Add(new{mode,value,error,historical.Estimates,historical.Opens});
    if(loader is IDisposable disposable)disposable.Dispose();
}
var json=JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true});
if(args.Length>0)await File.WriteAllTextAsync(args[0],json);
if(args.Length>1)
{
    using var expected=JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));using var actual=JsonDocument.Parse(json);
    if(!JsonElement.DeepEquals(expected.RootElement,actual.RootElement))throw new InvalidDataException("VX fallback differs from current C# oracle.");
}
Console.WriteLine($"PASS VX bootstrap: {results.Count} stored/history/invalid/cost/cancellation cases"+(args.Length>1?" exactly match the current Host oracle.":" recorded from the current Host."));
sealed class History(string mode,DateTimeOffset at):IMarketDataHistoricalProvider
{
    public int Estimates,Opens;
    public ValueTask<HistoricalProviderEstimate> EstimateAsync(HistoricalProviderRequest request,CancellationToken token){token.ThrowIfCancellationRequested();Estimates++;return ValueTask.FromResult(new HistoricalProviderEstimate(mode=="expensive"?.11m:0,1,1,at));}
    public ValueTask<IHistoricalRecordReader> OpenRangeAsync(HistoricalProviderRequest request,int maximumBatchRecords,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();Opens++;
        var row=new HistoricalProviderRecord{Kind=HistoricalProviderRecordKind.Trade,Symbol="VXX6",InstrumentId=mode=="invalid-id"?"invalid":"42",PublisherId="1",EventTimestampUtc=at,SourceSequence=77,CloseOrPrice=mode=="invalid-price"?-2:20.5m,VolumeOrSize=2,Action=mode=="invalid-action"?NormalizedTradeAction.Cancel:NormalizedTradeAction.New};
        HistoricalProviderRecord[] rows=mode=="empty"?[]:mode=="limit"?Enumerable.Repeat(row,100001).ToArray():[row];
        return ValueTask.FromResult<IHistoricalRecordReader>(new Reader(rows));
    }
    public ValueTask<HistoricalProviderJob> SubmitBatchAsync(HistoricalProviderRequest request,CancellationToken token)=>throw new InvalidOperationException("Unexpected batch.");
    public ValueTask<HistoricalProviderJob> GetBatchJobAsync(string id,CancellationToken token)=>throw new InvalidOperationException("Unexpected batch.");
    public ValueTask<IReadOnlyList<HistoricalProviderFile>> ListBatchFilesAsync(string id,CancellationToken token)=>throw new InvalidOperationException("Unexpected batch.");
    public ValueTask DownloadBatchFileAsync(string id,HistoricalProviderFile file,string path,CancellationToken token)=>throw new InvalidOperationException("Unexpected batch.");
    public ValueTask<IHistoricalRecordReader> OpenFileAsync(string path,HistoricalDataSchema schema,int count,CancellationToken token)=>throw new InvalidOperationException("Unexpected file.");
}
sealed class Reader(HistoricalProviderRecord[] rows):IHistoricalRecordReader
{
    bool done;
    public ValueTask<HistoricalProviderRecordBatch?> ReadNextAsync(CancellationToken token){token.ThrowIfCancellationRequested();if(done)return ValueTask.FromResult<HistoricalProviderRecordBatch?>(null);done=true;return ValueTask.FromResult<HistoricalProviderRecordBatch?>(new(rows,0,"fixture",true));}
    public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
}
