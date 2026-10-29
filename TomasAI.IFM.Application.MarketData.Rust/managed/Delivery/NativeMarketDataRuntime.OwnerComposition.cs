using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Attaches a persisted business plan name to native desired
    /// ownership. This command carries no context, source handle or renewal.</summary>
    public void AliasDatasetOwnerScope(Guid owner,Guid generation,string expected,string replacement)
    {
        var bits=GuidBits(owner);
        CommandDatasetOwner(JsonSerializer.SerializeToUtf8Bytes(new{command="scope-alias",owner=new[]{bits.Low,bits.High},
            generation=generation.ToByteArray().Select(value=>(int)value).ToArray(),expected,replacement}));
    }
    /// <summary>Copies native discovery admission, exclusions and immutable
    /// prepared contexts. This query does not prepare or renew a managed lease.</summary>
    public CompositionDiscoveryResult ReadDatasetOwnerDiscovery(Guid owner)
        {
        var result=QueryDatasetOwnerPreparedCache<NativeDiscoveryResult>(JsonSerializer.SerializeToUtf8Bytes(new{
            kind="scope-discovery",owner=owner.ToByteArray().Select(value=>(int)value).ToArray()}));
        return new(result.Lease,result.Exclusions,result.CompleteEmpty,result.Failure);
    }
    [MessagePack.MessagePackObject(AllowPrivate=true)]
    internal sealed record NativeDiscoveryResult([property:MessagePack.Key(0)]WorkerOptionChainRequest? Lease,
        [property:MessagePack.Key(1)]System.Collections.Immutable.ImmutableArray<TomasAI.IFM.Shared.MarketData.Contracts.Pricing.OptionPricingFailure> Exclusions,
        [property:MessagePack.Key(2)]bool CompleteEmpty,[property:MessagePack.Key(3)]TomasAI.IFM.Shared.MarketData.Contracts.Pricing.OptionPricingFailure? Failure);
    /// <summary>Copies native-prepared immutable context DTOs for an active
    /// logical scope. No C# reference preparation or provider lease is created.</summary>
    public WorkerOptionChainRequest? ReadDatasetOwnerScope(string scope,Guid lease,DateOnly maturity)
        =>QueryDatasetOwnerPreparedCache<WorkerOptionChainRequest?>(JsonSerializer.SerializeToUtf8Bytes(new{
            kind="scope-receipt",scope,lease=lease.ToByteArray().Select(value=>(int)value).ToArray(),maturity=maturity.DayNumber}));
    /// <summary>Requests one current monitored valuation. Rust chooses the
    /// exact scope, generation, API clock, quote policy and computation budget.</summary>
    public async Task<MarketCompositionSnapshot?> CaptureDatasetOwnerOptionRiskAsync(string contract,DateOnly valueDate,CancellationToken cancellationToken=default)
    {
        var command=JsonSerializer.SerializeToUtf8Bytes(new{kind="monitored-risk",contract,date=valueDate.DayNumber});
        var operation=SubmitOwnerResult(command,library.OwnerCapture??throw new NotSupportedException("Native option risk capture is unavailable."),"submit native option risk",cancellationToken);
        try{await operation.Completed.ConfigureAwait(false);cancellationToken.ThrowIfCancellationRequested();return TakeOwnerComposition(operation.Id).Snapshot;}
        finally{lock(gate)if(referenceOperations.Remove(operation.Id))NativeMarketDataLibrary.Check(library.ReferenceDiscard(handle.Identity,operation.Id),"discard native option risk result");}
    }
    /// <summary>Copies the existing public request to native source selection,
    /// metadata qualification, numerical capture and sealing. No C# context,
    /// Treasury, lease, local clock or pricing/source orchestration is invoked.</summary>
    public async Task<CompositionSnapshotResult> CaptureDatasetOwnerCompositionAsync(string dataset,CompositionSnapshotRequest request,CancellationToken cancellationToken=default,bool awaitQuotes=false)
    {
        ArgumentNullException.ThrowIfNull(request);cancellationToken.ThrowIfCancellationRequested();
        static int[] GuidBytes(Guid value)=>value.ToByteArray().Select(b=>(int)b).ToArray();
        static long Ticks(DateTimeOffset value)=>value.UtcTicks-DateTime.UnixEpoch.Ticks;
        var command=JsonSerializer.SerializeToUtf8Bytes(new{dataset,wait=awaitQuotes,id=GuidBytes(request.SnapshotId),scope=request.ScopeId,horizon=request.Horizon,generation=GuidBytes(request.GenerationId),
            at=Ticks(request.EvaluatedAtUtc),at_offset=(long)request.EvaluatedAtUtc.Offset.TotalMinutes,until=Ticks(request.DeadlineUtc),until_offset=(long)request.DeadlineUtc.Offset.TotalMinutes,
            options=request.IncludeOptions,maximum=request.MaximumContracts,age=request.MaximumQuoteAgeMilliseconds,skew=request.MaximumQuoteSkewMilliseconds,
            selection=request.SelectionOnly,allow_missing=request.AllowMissingOptionQuotes,risk=request.RiskContractIds??[],
            futures=(request.Futures.IsDefault?System.Collections.Immutable.ImmutableArray<CompositionFutureDefinition>.Empty:request.Futures).Select(f=>new object[]{f.ContractId,f.Root,f.Dataset,f.Exchange,f.Currency,Ticks(f.LastTradingUtc),(long)f.LastTradingUtc.Offset.TotalMinutes,
                f.Multiplier.ToString(CultureInfo.InvariantCulture),f.TickSize.ToString(CultureInfo.InvariantCulture),f.DefinitionDigest})});
        if(command.Length>2*1024*1024)throw new ArgumentOutOfRangeException(nameof(request),"Native capture command exceeds capacity.");
        var operation=SubmitOwnerResult(command,library.OwnerCapture??throw new NotSupportedException("Native composition capture is unavailable."),"submit native composition capture",cancellationToken);
        try
        {
            await operation.Completed.ConfigureAwait(false);cancellationToken.ThrowIfCancellationRequested();
            var result=TakeOwnerComposition(operation.Id);
            if(result is null || (result.Snapshot is null)==(result.Failure is null))throw new InvalidDataException("Native composition result is neither a snapshot nor a typed failure.");
            return result;
        }
        finally{lock(gate)if(referenceOperations.Remove(operation.Id))NativeMarketDataLibrary.Check(library.ReferenceDiscard(handle.Identity,operation.Id),"discard native composition result");}
    }
    unsafe CompositionSnapshotResult TakeOwnerComposition(ulong operation)
    {
        const int capacity=4*1024*1024+4096;var bytes=System.Buffers.ArrayPool<byte>.Shared.Rent(capacity);
        try
        {
            uint length;lock(gate){EnsureLiveAccess();fixed(byte* output=bytes)NativeMarketDataLibrary.Check(library.ReferenceTake(handle.Identity,operation,output,capacity,out length),"copy native composition result");}
            if(length is 0 or >capacity)throw new InvalidDataException("Native composition result exceeds capacity.");
            return MessagePack.MessagePackSerializer.Deserialize<CompositionSnapshotResult>(bytes.AsMemory(0,(int)length),MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
        }
        finally{System.Buffers.ArrayPool<byte>.Shared.Return(bytes,clearArray:true);}
    }
}
