using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;
public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Opens an independent read-only current-session trade replay. The
    /// caller supplies authoritative start time and resolved provider symbol;
    /// completion requires the provider's explicit replay marker. No existing
    /// futures/option lane is reset, and no managed provider thread is created.</summary>
    public unsafe IHistoricalRecordReader OpenFuturesReplay(string dataset,string symbol,string apiKey,string host,ushort port,
        string[] numericDnsServers,DateTimeOffset start,int maximumBatchRecords,TimeSpan budget,CancellationToken token=default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);ArgumentException.ThrowIfNullOrWhiteSpace(symbol);ArgumentNullException.ThrowIfNull(apiKey);ArgumentNullException.ThrowIfNull(numericDnsServers);
        if(dataset is not("GLBX.MDP3" or "XCBF.PITCH")||symbol.Length>70||apiKey.Length!=32||host.Length>253||numericDnsServers.Length is <1 or >4||port==0||maximumBatchRecords is <1 or >65536||budget<=TimeSpan.Zero||budget>TimeSpan.FromMinutes(2))throw new ArgumentException("Invalid bounded native replay configuration.");
        token.ThrowIfCancellationRequested();
        var input=JsonSerializer.SerializeToUtf8Bytes(new{dataset,symbol,key=apiKey,host,port,dns=numericDnsServers,start=checked((start.UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks)*100)});
        try
        {
            lock(gate)
            {
                EnsureLiveAccess();var operation=checked(++nextOperation);var request=new Pending{Control=new(TaskCreationOptions.RunContinuationsAsynchronously)};
                var reader=new HistoricalReader(this,operation,request.Control.Task,maximumBatchRecords,budget,token);pending.Add(operation,request);historyReaders.Add(operation,reader);
                int code;fixed(byte* bytes=input)code=library.HistoryReplay(handle.Identity,operation,bytes,(uint)input.Length,(uint)Math.Min(maximumBatchRecords,4096),(ulong)Math.Ceiling(budget.TotalMilliseconds));
                if(code!=0){pending.Remove(operation);historyReaders.Remove(operation);NativeMarketDataLibrary.Check(code,"open futures replay");}
                request.Cancellation=token.UnsafeRegister(_=>library.Cancel(handle.Identity,operation),null);return reader;
            }
        }
        finally{CryptographicOperations.ZeroMemory(input);}
    }
}
