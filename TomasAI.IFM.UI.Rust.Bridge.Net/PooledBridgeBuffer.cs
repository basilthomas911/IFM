using System.Buffers;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Temporary serialization storage; final publications always own a separate exact-sized array.</summary>
sealed class PooledBridgeBuffer : IBufferWriter<byte>, IDisposable
{
    static readonly ArrayPool<byte> Pool=ArrayPool<byte>.Create(256*1024,4);
    byte[] buffer=Pool.Rent(1024);
    int written;
    public void Advance(int count){if(count<0||count>buffer.Length-written)throw new ArgumentOutOfRangeException(nameof(count));written+=count;}
    public Memory<byte> GetMemory(int sizeHint=0){Ensure(sizeHint);return buffer.AsMemory(written);}
    public Span<byte> GetSpan(int sizeHint=0){Ensure(sizeHint);return buffer.AsSpan(written);}
    void Ensure(int size) {
        if(size<0)throw new ArgumentOutOfRangeException(nameof(size));size=Math.Max(size,1);
        if(buffer.Length-written>=size)return;
        var next=Pool.Rent(Math.Max(checked(written+size),checked(buffer.Length*2)));
        buffer.AsSpan(0,written).CopyTo(next);Pool.Return(buffer,clearArray:true);buffer=next;
    }
    public byte[] ToOwnedArray()=>buffer.AsSpan(0,written).ToArray();
    public void Dispose(){var old=buffer;buffer=[];written=0;if(old.Length>0)Pool.Return(old,clearArray:true);}
}
