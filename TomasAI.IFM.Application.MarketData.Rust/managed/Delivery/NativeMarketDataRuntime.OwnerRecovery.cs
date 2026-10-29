using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Copies the public request to Rust. Native code owns operation
    /// idempotence, stale-owner fencing, recovery and its original deadline.
    /// Cancelling this observer does not undo the accepted native command.</summary>
    public async Task<MarketDataRecoveryResult> RecoverDatasetOwnerAsync(MarketDataRecoveryRequest request,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(request);cancellationToken.ThrowIfCancellationRequested();
        static int[] GuidBytes(Guid value)=>value.ToByteArray().Select(b=>(int)b).ToArray();
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new{operation=GuidBytes(request.OperationId),worker=GuidBytes(request.ExpectedWorkerInstanceId),generation=GuidBytes(request.ExpectedGenerationId),role=request.Role,dataset=request.Dataset,date=OwnerDate(request.ValueDate)});
        var operation=SubmitOwnerRecovery(bytes,cancellationToken);
        try
        {
            await operation.Completed.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var result=TakeOwnerRecovery(operation.Id);
            if(result is null||result.OperationId!=request.OperationId||result.Role!=request.Role||result.Dataset!=request.Dataset)throw new InvalidDataException("Native recovery result identity differs from its request.");
            return result;
        }
        finally
        {
            lock(gate)if(referenceOperations.Remove(operation.Id))NativeMarketDataLibrary.Check(library.ReferenceDiscard(handle.Identity,operation.Id),"discard native recovery result");
        }
    }
    (ulong Id,Task Completed) SubmitOwnerRecovery(ReadOnlySpan<byte> command,CancellationToken token)
        =>SubmitOwnerResult(command,library.OwnerRecoveryRequest??throw new NotSupportedException("Native recovery request is unavailable."),"submit native recovery request",token);
    unsafe (ulong Id,Task Completed) SubmitOwnerResult(ReadOnlySpan<byte> command,NativeMarketDataLibrary.OwnerRecoveryRequestCall call,string label,CancellationToken token)
    {
        lock(gate)
        {
            EnsureLiveAccess();token.ThrowIfCancellationRequested();var operation=checked(++nextOperation);
            var request=new Pending{Control=new(TaskCreationOptions.RunContinuationsAsynchronously)};pending.Add(operation,request);
            int status;fixed(byte* input=command)status=call(handle.Identity,operation,input,(uint)command.Length);
            if(status!=0){pending.Remove(operation);NativeMarketDataLibrary.Check(status,label);}
            referenceOperations.Add(operation);request.Cancellation=token.UnsafeRegister(_=>library.Cancel(handle.Identity,operation),null);
            return(operation,request.Control.Task);
        }
    }
    unsafe MarketDataRecoveryResult TakeOwnerRecovery(ulong operation)
    {
        var bytes=System.Buffers.ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            uint length;lock(gate){EnsureLiveAccess();fixed(byte* output=bytes)NativeMarketDataLibrary.Check(library.ReferenceTake(handle.Identity,operation,output,8192,out length),"copy native recovery result");}
            if(length is 0 or >8192)throw new InvalidDataException("Native recovery result exceeds capacity.");
            return MessagePack.MessagePackSerializer.Deserialize<MarketDataRecoveryResult>(bytes.AsMemory(0,(int)length),MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
        }
        finally{System.Buffers.ArrayPool<byte>.Shared.Return(bytes,clearArray:true);}
    }
}
