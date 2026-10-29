using System.Buffers;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

public sealed partial class NativeMarketDataRuntime
{
    unsafe (ulong Operation,Task Completed) SubmitRetained(NativeMarketDataLibrary.ColdSubmitCall call,uint mode,byte[] query,TimeSpan budget,CancellationToken token)
    {
        lock(gate){EnsureLiveAccess();token.ThrowIfCancellationRequested();var operation=checked(++nextOperation);var request=new Pending{Control=new(TaskCreationOptions.RunContinuationsAsynchronously)};pending.Add(operation,request);
            int code;fixed(byte* input=query)code=call(handle.Identity,operation,mode,input,(uint)query.Length,(ulong)Math.Ceiling(budget.TotalMilliseconds));
            if(code!=0){pending.Remove(operation);NativeMarketDataLibrary.Check(code,"submit FMP reference query");}
            referenceOperations.Add(operation);request.Cancellation=token.UnsafeRegister(_=>library.Cancel(handle.Identity,operation),null);return(operation,request.Control.Task);
        }
    }

    unsafe JsonDocument TakeRetainedDocument(ulong operation)
    {
        const int maximum=16*1024*1024;
        for(var capacity=65536;capacity<=maximum;capacity*=2){
            var buffer=ArrayPool<byte>.Shared.Rent(capacity);
            try{
                int code;uint length;lock(gate){EnsureLiveAccess();fixed(byte* output=buffer)code=library.ReferenceTake(handle.Identity,operation,output,(uint)capacity,out length);}
                if(code==4&&capacity<maximum)continue;NativeMarketDataLibrary.Check(code,"copy retained result");if(length>capacity)throw new InvalidDataException("Native result exceeded transfer capacity.");
                // Parse(Stream) owns a copy; returning a pooled transfer buffer
                // must never invalidate a JsonDocument still used by its caller.
                using var input=new MemoryStream(buffer,0,checked((int)length),writable:false);
                return JsonDocument.Parse(input);
            }finally{ArrayPool<byte>.Shared.Return(buffer,true);}
        }throw new InvalidDataException("Native result exceeded transfer capacity.");
    }
}
