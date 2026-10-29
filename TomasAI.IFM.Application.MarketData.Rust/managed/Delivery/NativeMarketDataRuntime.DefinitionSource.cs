using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Shared.MarketData.Definitions;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;
public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Maps one exact raw definition through Rust and copies its parsed
    /// contract fields into the unchanged shared DTO. Raw JSON identity is retained;
    /// no managed provider parser or numerical calculation is invoked.</summary>
    public unsafe ExactInstrumentDefinition ProjectDefinition(string dataset,string source)
    {
        ArgumentNullException.ThrowIfNull(dataset);ArgumentNullException.ThrowIfNull(source);
        if(dataset.Length>64||source.Length>65536||Encoding.UTF8.GetByteCount(dataset)>64||Encoding.UTF8.GetByteCount(source)>65536)throw new ArgumentOutOfRangeException(nameof(source));
        var datasetBytes=Encoding.UTF8.GetBytes(dataset);var sourceBytes=Encoding.UTF8.GetBytes(source);var buffer=ArrayPool<byte>.Shared.Rent(262144);
        try{
            int code;uint length;lock(gate){EnsureLiveAccess();fixed(byte* d=datasetBytes)fixed(byte* s=sourceBytes)fixed(byte* output=buffer)code=library.DefinitionProject(d,(uint)datasetBytes.Length,s,(uint)sourceBytes.Length,output,262144,out length);}
            if(code==1)throw new InvalidDataException("Native source instrument definition is invalid.");NativeMarketDataLibrary.Check(code,"project instrument definition");if(length>262144)throw new InvalidDataException("Native definition projection exceeded capacity.");
            var row=JsonSerializer.Deserialize<NativeContractDefinition>(buffer.AsSpan(0,(int)length))??throw new InvalidDataException("Missing native definition projection.");
            var summary=new ContractDetail{Dataset=row.Dataset,RawSymbol=row.Symbol,Ticker=row.Asset,Underlying=row.Underlying,Instrument=new(row.Publisher,row.Instrument),ContractKind=(ContractKind)row.Kind,UnderlyingInstrumentId=row.UnderlyingInstrument,ContractMultiplier=row.Multiplier,StrikePrice=row.Strike,MinimumPriceIncrement=row.Tick,MinimumPriceIncrementAmount=row.TickAmount,ExpirationTimestampNanoseconds=row.Expiration,ActivationTimestampNanoseconds=row.Activation,MaturityDate=row.Maturity,MaturityWeek=row.MaturityWeek,Currency=row.Currency,SettlementCurrency=row.SettlementCurrency,Exchange=row.Exchange,SecurityType=row.SecurityType,Cfi=row.Cfi,UnitOfMeasure=row.UnitOfMeasure};
            return new(){Dataset=row.Dataset,Json=source,PublisherId=row.Publisher,InstrumentId=row.Instrument,RawSymbol=row.Symbol,Asset=row.Asset,InstrumentClass=row.Class,Currency=row.Currency,Exchange=row.Exchange,ReceivedNanoseconds=row.ReceivedNanoseconds,EventNanoseconds=row.EventNanoseconds,Deleted=row.Deleted,Summary=summary};
        }finally{ArrayPool<byte>.Shared.Return(buffer,true);}
    }
    internal unsafe DefinitionReader OpenDefinitions(string dataset,TimeSpan budget,CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);token.ThrowIfCancellationRequested();var input=Encoding.UTF8.GetBytes(dataset);if(input.Length>64)throw new ArgumentOutOfRangeException(nameof(dataset));
        lock(gate){EnsureLiveAccess();var operation=checked(++nextOperation);var request=new Pending{Control=new(TaskCreationOptions.RunContinuationsAsynchronously)};var reader=new DefinitionReader(this,operation,request.Control.Task,budget,token);pending.Add(operation,request);historyReaders.Add(operation,reader);
            int code;fixed(byte* bytes=input)code=library.DefinitionsOpen(handle.Identity,operation,bytes,(uint)input.Length,(ulong)Math.Ceiling(budget.TotalMilliseconds));
            if(code!=0){pending.Remove(operation);historyReaders.Remove(operation);NativeMarketDataLibrary.Check(code,"open full definition source");}
            request.Cancellation=token.UnsafeRegister(_=>library.Cancel(handle.Identity,operation),null);return reader;
        }
    }
    internal sealed class DefinitionReader(NativeMarketDataRuntime runtime,ulong operation,Task completed,TimeSpan budget,CancellationToken caller):IAsyncDisposable,HistoricalOwner
    {
        public ulong Operation=>operation;
        public Channel<byte> Pulse{get;}=Channel.CreateBounded<byte>(new BoundedChannelOptions(1){FullMode=BoundedChannelFullMode.DropWrite,SingleReader=true,AllowSynchronousContinuations=false});
        public Channel<byte> InputPulse{get;}=Channel.CreateBounded<byte>(1);
        readonly CancellationTokenSource stopping=new();
        readonly long started=Stopwatch.GetTimestamp();
        bool ended;
        readonly object disposalGate=new();Task? disposal;
        public void CancelInput()=>stopping.Cancel();
        public Task JoinInputAsync()=>Task.CompletedTask;
        internal async Task<int> ReadNextAsync(byte[] bytes,CancellationToken token)
        {
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,caller,stopping.Token);
            while(true){linked.Token.ThrowIfCancellationRequested();if(completed.IsFaulted||completed.IsCanceled)await completed.ConfigureAwait(false);
                if(ended){await completed.ConfigureAwait(false);return -1;}
                var chunk=runtime.DrainHistoricalDownload(operation,bytes);ended=chunk.Ended;if(chunk.Count>0)return chunk.Count;
                if(!ended)try{await Pulse.Reader.ReadAsync(linked.Token).ConfigureAwait(false);}catch(ChannelClosedException){await completed.ConfigureAwait(false);throw new ObjectDisposedException(nameof(DefinitionReader));}
            }
        }
        public ValueTask DisposeAsync(){lock(disposalGate)return new(disposal??=DisposeCoreAsync());}
        async Task DisposeCoreAsync()
        {
            stopping.Cancel();runtime.CloseHistoricalReader(operation);var remaining=budget-Stopwatch.GetElapsedTime(started);remaining=remaining>TimeSpan.Zero?remaining:TimeSpan.Zero;
            if(remaining>TimeSpan.FromMinutes(2))remaining=TimeSpan.FromMinutes(2);
            try{await completed.WaitAsync(remaining).ConfigureAwait(false);}catch when(completed.IsCompleted){_=completed.Exception;}
        }
    }
}
