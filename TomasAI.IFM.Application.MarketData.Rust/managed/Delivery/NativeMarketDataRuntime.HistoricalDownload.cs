using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;
public sealed partial class NativeMarketDataRuntime
{
    internal unsafe Task DownloadHistoricalFileAsync(string job,HistoricalProviderFile file,string destination,TimeSpan budget,CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(job);ArgumentNullException.ThrowIfNull(file);token.ThrowIfCancellationRequested();
        if(file.SizeBytes<0||string.IsNullOrWhiteSpace(file.FileName)||string.IsNullOrWhiteSpace(file.Sha256))throw new ArgumentException("An immutable historical file descriptor is required.",nameof(file));
        var input=JsonSerializer.SerializeToUtf8Bytes(new{job,filename=file.FileName,size=file.SizeBytes,hash=file.Sha256});if(input.Length>16384)throw new ArgumentOutOfRangeException(nameof(file));
        lock(gate){EnsureLiveAccess();var operation=checked(++nextOperation);var request=new Pending{Control=new(TaskCreationOptions.RunContinuationsAsynchronously)};
            var owner=new HistoricalDownload(this,operation,request.Control.Task,file,Path.GetFullPath(destination),budget,token);pending.Add(operation,request);historyReaders.Add(operation,owner);
            int code;fixed(byte* bytes=input)code=library.HistoryDownload(handle.Identity,operation,bytes,(uint)input.Length,(ulong)Math.Ceiling(budget.TotalMilliseconds));
            if(code!=0){pending.Remove(operation);historyReaders.Remove(operation);NativeMarketDataLibrary.Check(code,"open historical download");}
            request.Cancellation=token.UnsafeRegister(_=>library.Cancel(handle.Identity,operation),null);return owner.Start();
        }
    }
    unsafe (int Count,bool Ended) DrainHistoricalDownload(ulong operation,byte[] bytes)
    {lock(gate){EnsureLiveAccess();uint length,ended;fixed(byte* output=bytes)NativeMarketDataLibrary.Check(library.HistoryDownloadTake(handle.Identity,operation,output,65536,out length,out ended),"copy historical download chunk");if(length>65536||ended>1)throw new InvalidDataException("Malformed native download chunk.");return((int)length,ended==1);}}
    sealed class HistoricalDownload(NativeMarketDataRuntime runtime,ulong operation,Task completed,HistoricalProviderFile descriptor,string destination,TimeSpan budget,CancellationToken caller):HistoricalOwner
    {
        public ulong Operation=>operation;
        public Channel<byte> Pulse{get;}=Channel.CreateBounded<byte>(new BoundedChannelOptions(1){FullMode=BoundedChannelFullMode.DropWrite,SingleReader=true,AllowSynchronousContinuations=false});
        public Channel<byte> InputPulse{get;}=Channel.CreateBounded<byte>(1);
        readonly CancellationTokenSource stopping=new();
        readonly long started=Stopwatch.GetTimestamp();
        Task? sink;
        internal Task Start()
        {
            var writing=Task.Run(ExecuteAsync);
            return sink=writing.ContinueWith(task=>{try{task.GetAwaiter().GetResult();}finally{runtime.CloseHistoricalReader(operation);}},CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
        }
        public void CancelInput()=>stopping.Cancel();
        public async Task JoinInputAsync(){if(sink is{} pending)try{await pending.ConfigureAwait(false);}catch when(pending.IsCompleted){_=pending.Exception;}}
        async Task ExecuteAsync()
        {
            using var bounded=CancellationTokenSource.CreateLinkedTokenSource(caller,stopping.Token);var remaining=budget-Stopwatch.GetElapsedTime(started);bounded.CancelAfter(remaining>TimeSpan.Zero?remaining:TimeSpan.Zero);var token=bounded.Token;
            var temporary=destination+"."+Guid.NewGuid().ToString("N")+".partial";var promoted=false;Exception? failure=null;
            var bytes=ArrayPool<byte>.Shared.Rent(65536);
            try{
                token.ThrowIfCancellationRequested();Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                long size=0;using var checksum=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.Asynchronous|FileOptions.SequentialScan)){
                    while(true){
                        token.ThrowIfCancellationRequested();if(completed.IsFaulted||completed.IsCanceled)await completed.ConfigureAwait(false);
                        var chunk=runtime.DrainHistoricalDownload(operation,bytes);
                        if(chunk.Count>0){size=checked(size+chunk.Count);if(size>descriptor.SizeBytes)throw new InvalidDataException("Historical download exceeds its immutable size.");checksum.AppendData(bytes,0,chunk.Count);await output.WriteAsync(bytes.AsMemory(0,chunk.Count),token).ConfigureAwait(false);}
                        if(chunk.Ended){await completed.ConfigureAwait(false);break;}
                        if(chunk.Count==0)try{await Pulse.Reader.ReadAsync(token).ConfigureAwait(false);}catch(ChannelClosedException){await completed.ConfigureAwait(false);throw new ObjectDisposedException(nameof(HistoricalDownload));}
                    }
                    await output.FlushAsync(token).ConfigureAwait(false);
                }
                if(size!=descriptor.SizeBytes)throw new InvalidDataException("Historical download size differs from its immutable descriptor.");
                var actual=Convert.ToHexStringLower(checksum.GetHashAndReset());
                if(descriptor.Sha256.Length==64&&descriptor.Sha256.All(Uri.IsHexDigit)&&!actual.Equals(descriptor.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Historical storage checksum differs from its immutable descriptor.");
                token.ThrowIfCancellationRequested();File.Move(temporary,destination,true);promoted=true;
            }catch(OperationCanceledException error)when(!caller.IsCancellationRequested&&!stopping.IsCancellationRequested){failure=error;throw new TimeoutException("Original historical download deadline expired.",error);}
            catch(Exception error){failure=error;throw;}
            finally{
                ArrayPool<byte>.Shared.Return(bytes,true);
                if(!promoted&&File.Exists(temporary))try{File.Delete(temporary);}catch(Exception cleanup){if(failure is not null)throw new AggregateException("Historical download and provisional-file cleanup failed.",failure,cleanup);throw;}
            }
        }
    }
}
