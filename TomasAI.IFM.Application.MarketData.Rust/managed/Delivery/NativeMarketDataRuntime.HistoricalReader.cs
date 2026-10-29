using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

public sealed partial class NativeMarketDataRuntime
{
    interface HistoricalOwner
    {
        ulong Operation{get;}
        Channel<byte> Pulse{get;}
        Channel<byte> InputPulse{get;}
        void CancelInput();
        Task JoinInputAsync();
    }
    readonly Dictionary<ulong,HistoricalOwner> historyReaders=[];
    internal unsafe IHistoricalRecordReader OpenHistoricalRange(HistoricalProviderRequest request,int maximumBatchRecords,TimeSpan budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(maximumBatchRecords is <1 or >65536)throw new ArgumentOutOfRangeException(nameof(maximumBatchRecords));
        if(budget<=TimeSpan.Zero||budget>TimeSpan.FromMinutes(30))throw new ArgumentOutOfRangeException(nameof(budget));var input=HistoricalQuery(request);
        lock(gate){EnsureLiveAccess();var operation=checked(++nextOperation);var pendingRequest=new Pending{Control=new(TaskCreationOptions.RunContinuationsAsynchronously)};
            var reader=new HistoricalReader(this,operation,pendingRequest.Control.Task,maximumBatchRecords,budget,token);pending.Add(operation,pendingRequest);historyReaders.Add(operation,reader);
            int code;fixed(byte* bytes=input)code=library.HistoryRange(handle.Identity,operation,bytes,(uint)input.Length,(uint)Math.Min(maximumBatchRecords,4096),(ulong)Math.Ceiling(budget.TotalMilliseconds));
            if(code!=0){pending.Remove(operation);historyReaders.Remove(operation);NativeMarketDataLibrary.Check(code,"open historical range");}
            pendingRequest.Cancellation=token.UnsafeRegister(_=>library.Cancel(handle.Identity,operation),null);return reader;
        }
    }
    internal unsafe IHistoricalRecordReader OpenHistoricalFile(string path,HistoricalDataSchema schema,int maximumBatchRecords,TimeSpan budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(maximumBatchRecords is <1 or >65536)throw new ArgumentOutOfRangeException(nameof(maximumBatchRecords));
        var input=JsonSerializer.SerializeToUtf8Bytes(new{schema=(byte)schema,fallback=Path.GetFileNameWithoutExtension(path)});if(input.Length>8192)throw new ArgumentOutOfRangeException(nameof(path));
        lock(gate){EnsureLiveAccess();var operation=checked(++nextOperation);var request=new Pending{Control=new(TaskCreationOptions.RunContinuationsAsynchronously)};
            var reader=new HistoricalReader(this,operation,request.Control.Task,maximumBatchRecords,budget,token);pending.Add(operation,request);historyReaders.Add(operation,reader);
            int code;fixed(byte* bytes=input)code=library.HistoryFile(handle.Identity,operation,bytes,(uint)input.Length,(uint)Math.Min(maximumBatchRecords,4096),(ulong)Math.Ceiling(budget.TotalMilliseconds));
            if(code!=0){pending.Remove(operation);historyReaders.Remove(operation);NativeMarketDataLibrary.Check(code,"open historical file decoder");}
            request.Cancellation=token.UnsafeRegister(_=>library.Cancel(handle.Identity,operation),null);
            reader.StartFileInput(path,token);return reader;
        }
    }
    unsafe int PushHistoricalFile(ulong operation,byte[] bytes,int length)
    {lock(gate){EnsureLiveAccess();fixed(byte* input=bytes){var code=library.HistoryFilePush(handle.Identity,operation,input,(uint)length);if(code is not(0 or 2 or 4))NativeMarketDataLibrary.Check(code,"feed historical file");return code;}}}
    void SignalHistoricalInputs(){lock(gate)foreach(var reader in historyReaders.Values)reader.InputPulse.Writer.TryWrite(1);}
    void SignalHistoricalReaders(){lock(gate)foreach(var reader in historyReaders.Values)reader.Pulse.Writer.TryWrite(1);}
    void SignalHistoricalCompletion(ulong operation){lock(gate)if(historyReaders.TryGetValue(operation,out var reader)){reader.Pulse.Writer.TryWrite(1);reader.InputPulse.Writer.TryWrite(1);}}
    void CompleteHistoricalReaders(Exception? error){lock(gate)foreach(var reader in historyReaders.Values){reader.Pulse.Writer.TryComplete(error);reader.InputPulse.Writer.TryComplete(error);}}
    void CancelHistoricalReader(ulong operation){lock(gate)if(historyReaders.ContainsKey(operation)){var code=library.Cancel(handle.Identity,operation);if(code!=1)NativeMarketDataLibrary.Check(code,"cancel historical reader");}}
    void CloseHistoricalReader(ulong operation){lock(gate)if(historyReaders.Remove(operation,out var reader)){NativeMarketDataLibrary.Check(library.HistoryReaderClose(handle.Identity,operation),"close historical reader");reader.Pulse.Writer.TryComplete();}}
    async Task DiscardHistoricalReadersAsync(){HistoricalOwner[] readers;lock(gate){readers=historyReaders.Values.ToArray();foreach(var reader in readers){reader.CancelInput();NativeMarketDataLibrary.Check(library.HistoryReaderClose(handle.Identity,reader.Operation),"discard historical reader");reader.Pulse.Writer.TryComplete();reader.InputPulse.Writer.TryComplete();}historyReaders.Clear();}await Task.WhenAll(readers.Select(x=>x.JoinInputAsync())).ConfigureAwait(false);}
    unsafe (int Count,bool Ended) DrainHistorical(ulong operation,NativeHistoricalRecord[] output)
    {
        lock(gate){EnsureLiveAccess();uint count,ended;fixed(NativeHistoricalRecord* rows=output)NativeMarketDataLibrary.Check(library.HistoryRecords(handle.Identity,operation,rows,4096,out count,out ended),"read historical rows");if(count>4096||ended>1)throw new InvalidDataException("Malformed native historical batch.");return((int)count,ended==1);}
    }
    sealed class HistoricalReader(NativeMarketDataRuntime runtime,ulong operation,Task completed,int maximum,TimeSpan budget,CancellationToken operationToken):IHistoricalRecordReader,HistoricalOwner
    {
        public ulong Operation=>operation;
        public Channel<byte> Pulse{get;}=Channel.CreateBounded<byte>(new BoundedChannelOptions(1){FullMode=BoundedChannelFullMode.DropWrite,SingleReader=true,AllowSynchronousContinuations=false});
        public Channel<byte> InputPulse{get;}=Channel.CreateBounded<byte>(new BoundedChannelOptions(1){FullMode=BoundedChannelFullMode.DropWrite,SingleReader=true,AllowSynchronousContinuations=false});
        Task? input;
        readonly Queue<NativeHistoricalRecord> rows=new(4096);
        readonly SemaphoreSlim serial=new(1,1);
        readonly CancellationTokenSource stopping=new();
        readonly long started=Stopwatch.GetTimestamp();
        readonly object disposalGate=new();
        Task? disposal;
        long ordinal;
        bool ended,finished;
        internal void StartFileInput(string path,CancellationToken token)=>input=Task.Run(()=>FeedFileAsync(path,token));
        public void CancelInput()=>stopping.Cancel();
        public async Task JoinInputAsync(){if(input is{} source)try{await source.ConfigureAwait(false);}catch when(source.IsCompleted){_=source.Exception;}}
        async Task FeedFileAsync(string path,CancellationToken caller)
        {
            using var bounded=CancellationTokenSource.CreateLinkedTokenSource(caller,stopping.Token);
            var remaining=budget-Stopwatch.GetElapsedTime(started);bounded.CancelAfter(remaining>TimeSpan.Zero?remaining:TimeSpan.Zero);var token=bounded.Token;
            var bytes=ArrayPool<byte>.Shared.Rent(65536);
            try{
                token.ThrowIfCancellationRequested();await using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,65536,FileOptions.Asynchronous|FileOptions.SequentialScan);
                while(true){var count=await file.ReadAsync(bytes.AsMemory(0,65536),token).ConfigureAwait(false);
                    while(true){token.ThrowIfCancellationRequested();if(completed.IsCompleted){await completed.ConfigureAwait(false);throw new InvalidDataException("File decoder stopped before source EOF.");}
                        var code=runtime.PushHistoricalFile(operation,bytes,count);if(code==0)break;if(code==2){await completed.ConfigureAwait(false);throw new InvalidDataException("File input channel closed before EOF.");}
                        await InputPulse.Reader.ReadAsync(token).ConfigureAwait(false);
                    }if(count==0)break;
                }
            }catch{runtime.CancelHistoricalReader(operation);throw;}
            finally{ArrayPool<byte>.Shared.Return(bytes,true);}
        }
        async Task<bool> Available(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            while(rows.Count==0){
                token.ThrowIfCancellationRequested();if(input is{IsFaulted:true} failedInput)await failedInput.ConfigureAwait(false);if(completed.IsCanceled&&input is{} canceledInput){var remaining=budget-Stopwatch.GetElapsedTime(started);await canceledInput.WaitAsync(remaining>TimeSpan.Zero?remaining:TimeSpan.Zero,token).ConfigureAwait(false);}
                if(completed.IsFaulted||completed.IsCanceled)await completed.ConfigureAwait(false);
                if(ended){await completed.ConfigureAwait(false);if(input is{} source)await source.ConfigureAwait(false);return false;}
                var buffer=ArrayPool<NativeHistoricalRecord>.Shared.Rent(4096);
                try{var batch=runtime.DrainHistorical(operation,buffer);ended=batch.Ended;for(var i=0;i<batch.Count;i++)rows.Enqueue(buffer[i]);}
                finally{ArrayPool<NativeHistoricalRecord>.Shared.Return(buffer,true);}
                if(rows.Count>0)return true;
                if(!ended)try{await Pulse.Reader.ReadAsync(token).ConfigureAwait(false);}catch(ChannelClosedException){await completed.ConfigureAwait(false);throw new ObjectDisposedException(nameof(HistoricalReader));}
            }return true;
        }
        public async ValueTask<HistoricalProviderRecordBatch?> ReadNextAsync(CancellationToken cancellationToken)
        {
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,stopping.Token,operationToken);var token=linked.Token;
            using var cancellation=token.UnsafeRegister(_=>runtime.CancelHistoricalReader(operation),null);
            await serial.WaitAsync(token).ConfigureAwait(false);
            try{
                ObjectDisposedException.ThrowIf(stopping.IsCancellationRequested,this);if(finished)return null;
                var result=new List<HistoricalProviderRecord>(maximum);
                while(result.Count<maximum&&await Available(token).ConfigureAwait(false))result.Add(Convert(rows.Dequeue()));
                finished=!await Available(token).ConfigureAwait(false);
                var next=checked(ordinal++);return new(result.ToArray(),next,next.ToString(CultureInfo.InvariantCulture),finished);
            }finally{serial.Release();}
        }
        public ValueTask DisposeAsync(){lock(disposalGate)return new(disposal??=DisposeCoreAsync());}
        async Task DisposeCoreAsync()
        {
            // Cancel native work before joining. A repeated dispose cannot renew
            // the original acquisition deadline or manufacture successful cleanup.
            try{stopping.Cancel();}finally{runtime.CloseHistoricalReader(operation);}
            var remaining=budget-Stopwatch.GetElapsedTime(started);if(remaining<TimeSpan.Zero)remaining=TimeSpan.Zero;
            var joined=JoinInputAsync();await joined.WaitAsync(remaining).ConfigureAwait(false);
            remaining=budget-Stopwatch.GetElapsedTime(started);if(remaining<TimeSpan.Zero)remaining=TimeSpan.Zero;
            try{await completed.WaitAsync(remaining).ConfigureAwait(false);}catch when(completed.IsCompleted){_=completed.Exception;}
        }
        static unsafe HistoricalProviderRecord Convert(NativeHistoricalRecord row)
        {
            if(row.Size!=120||row.Abi!=1||row.Kind is not(2 or 3))throw new InvalidDataException("Malformed native historical row.");
            for(var i=0;i<6;i++)if(row.Reserved[i]!=0)throw new InvalidDataException("Historical reserved bytes are nonzero.");
            var length=0;while(length<32&&row.Symbol[length]!=0)length++;
            return new(){Kind=(HistoricalProviderRecordKind)row.Kind,Symbol=Encoding.UTF8.GetString(new ReadOnlySpan<byte>(row.Symbol,length)),InstrumentId=row.Instrument.ToString(CultureInfo.InvariantCulture),PublisherId=row.Publisher.ToString(CultureInfo.InvariantCulture),EventTimestampUtc=DateTimeOffset.UnixEpoch.AddTicks(row.Event/100L),SourceSequence=row.Sequence,Open=row.Open/1_000_000_000m,High=row.High/1_000_000_000m,Low=row.Low/1_000_000_000m,CloseOrPrice=row.Price/1_000_000_000m,VolumeOrSize=checked((long)row.Quantity),Action=row.Action==(byte)'T'?NormalizedTradeAction.New:NormalizedTradeAction.Unknown,Side=row.Side==(byte)'B'?NormalizedTradeSide.Buy:row.Side==(byte)'A'?NormalizedTradeSide.Sell:NormalizedTradeSide.Unknown,Conditions=(NormalizedTradeConditionFlags)row.Conditions};
        }
    }
}
