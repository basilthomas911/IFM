using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;
public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Commands a native-owned committed-journal refresh. Rust owns
    /// source loading, cancellation, exact ownership, conditional delivery and
    /// recovery. This observer has no managed lease/gate/renewal/recovery clock.</summary>
    public Task<DurableRealization?> ReconcileDatasetOwnerCommittedAsync(CancellationToken token=default)
        =>BusinessCommandAsync<DurableRealization?>(new{kind="committed-reconcile"},token);
    /// <summary>Copies a logical release. Committed owner messages are checked
    /// against native-accepted journal facts; they cannot invent or remove leases.</summary>
    public Task<WorkerOptionChainResult> ReleaseDatasetOwnerCompositionAsync(string dataset,WorkerOptionChainRelease request,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ownership=request.Ownership;
        return BusinessCommandAsync<WorkerOptionChainResult>(new{kind="composition-release",dataset,scope=request.ScopeId,
            lease=request.LeaseId.ToByteArray().Select(value=>(int)value),generation=request.GenerationId.ToByteArray().Select(value=>(int)value),
            ownership=ownership is null?null:new{authority=ownership.AuthorityScope,revision=ownership.Revision,digest=ownership.ContractSetDigest,
                owners=ownership.Owners.IsDefault?null:ownership.Owners.Select(owner=>owner is null?null:new{id=owner.LeaseId.ToString("N"),contracts=owner.ContractIds.IsDefault?(System.Collections.Immutable.ImmutableArray<string>?)null:owner.ContractIds})}},token);
    }
    async Task<T> BusinessCommandAsync<T>(object input,CancellationToken token,int maximum=65536)
    {
        token.ThrowIfCancellationRequested();if(!SupportsDatasetOwnerBusiness)throw new NotSupportedException("The native library lacks committed business ownership commands.");
        var command=JsonSerializer.SerializeToUtf8Bytes(input);if(command.Length>2*1024*1024)throw new ArgumentOutOfRangeException(nameof(input));
        var operation=SubmitOwnerResult(command,library.OwnerCapture!,"submit native business command",token);
        try{await operation.Completed.ConfigureAwait(false);token.ThrowIfCancellationRequested();return TakeBusiness<T>(operation.Id,maximum);}
        finally{lock(gate)if(referenceOperations.Remove(operation.Id))NativeMarketDataLibrary.Check(library.ReferenceDiscard(handle.Identity,operation.Id),"discard native business result");}
    }
    unsafe T TakeBusiness<T>(ulong operation,int maximum)
    {
        for(var capacity=65536;capacity<=maximum;capacity=checked(capacity*2)) {
            var buffer=System.Buffers.ArrayPool<byte>.Shared.Rent(capacity);
            try {
                int code;uint length;
                lock(gate){EnsureLiveAccess();fixed(byte* output=buffer)code=library.ReferenceTake(handle.Identity,operation,output,(uint)capacity,out length);}
                if(code==4&&capacity<maximum)continue;
                NativeMarketDataLibrary.Check(code,"copy native business result");
                if(length==0||length>capacity)throw new InvalidDataException("Native business result exceeds capacity.");
                return MessagePack.MessagePackSerializer.Deserialize<T>(buffer.AsMemory(0,(int)length),MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
            }finally{System.Buffers.ArrayPool<byte>.Shared.Return(buffer,clearArray:true);}
        }
        throw new InvalidDataException("Native business result exceeds capacity.");
    }
}
