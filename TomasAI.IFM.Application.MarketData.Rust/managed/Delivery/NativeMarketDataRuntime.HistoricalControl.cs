using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

public sealed partial class NativeMarketDataRuntime
{
    static byte[] HistoricalQuery(HistoricalProviderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if(string.IsNullOrWhiteSpace(request.Dataset)||request.Symbols is not{Length:>0 and <=2048}||request.Symbols.Any(string.IsNullOrWhiteSpace)||request.StartUtc>=request.EndUtc||request.RecordLimit<0)throw new ArgumentException("Historical provider range and symbols must be valid.",nameof(request));
        static long Nanoseconds(DateTimeOffset time)=>checked((time.UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks)*100L);
        var input=JsonSerializer.SerializeToUtf8Bytes(new{dataset=request.Dataset,symbols=request.Symbols,schema=(byte)request.Schema,symbology=(byte)request.Symbology,start=Nanoseconds(request.StartUtc),end=Nanoseconds(request.EndUtc),limit=request.RecordLimit});
        if(input.Length>262144)throw new ArgumentOutOfRangeException(nameof(request));return input;
    }
    internal async Task<JsonDocument> ReadHistoricalControlAsync(uint mode,HistoricalProviderRequest? request,string? job,TimeSpan budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(budget<TimeSpan.Zero||budget>TimeSpan.FromMinutes(30))throw new ArgumentOutOfRangeException(nameof(budget));
        byte[] input;
        if(mode<=1 || mode==4)input=HistoricalQuery(request??throw new ArgumentNullException(nameof(request)));
        else{ArgumentException.ThrowIfNullOrWhiteSpace(job);input=JsonSerializer.SerializeToUtf8Bytes(new{job});}
        var started=Stopwatch.GetTimestamp();var operation=SubmitRetained(library.HistoryControl,mode,input,budget,token);
        try{await operation.Completed.ConfigureAwait(false);token.ThrowIfCancellationRequested();if(Stopwatch.GetElapsedTime(started)>=budget)throw new TimeoutException("Original historical control deadline expired.");return TakeRetainedDocument(operation.Operation);}
        finally{lock(gate)if(referenceOperations.Remove(operation.Operation))Interop.NativeMarketDataLibrary.Check(library.ReferenceDiscard(handle.Identity,operation.Operation),"discard historical control result");}
    }
}
