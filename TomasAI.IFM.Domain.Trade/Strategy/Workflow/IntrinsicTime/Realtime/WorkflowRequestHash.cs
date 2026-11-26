using System.Buffers;
using System.Security.Cryptography;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

/// <summary>Hashes the exact MessagePack encoding without creating a contiguous request byte array.</summary>
internal sealed class WorkflowRequestHash : IBufferWriter<byte>, IDisposable
{
    readonly IncrementalHash digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    byte[] buffer=ArrayPool<byte>.Shared.Rent(16384);
    public static string Compute<T>(T request)
    {
        using var writer=new WorkflowRequestHash();
        MessagePackSerializer.Serialize(writer,request);
        return Convert.ToHexString(writer.digest.GetHashAndReset());
    }
    public void Advance(int count)
    {
        if(count<0||count>buffer.Length)throw new ArgumentOutOfRangeException(nameof(count));
        digest.AppendData(buffer.AsSpan(0,count));
    }
    public Memory<byte> GetMemory(int sizeHint=0){Ensure(sizeHint);return buffer;}
    public Span<byte> GetSpan(int sizeHint=0){Ensure(sizeHint);return buffer;}
    void Ensure(int sizeHint)
    {
        if(sizeHint<0)throw new ArgumentOutOfRangeException(nameof(sizeHint));
        if(sizeHint<=buffer.Length)return;
        var next=ArrayPool<byte>.Shared.Rent(sizeHint);
        ArrayPool<byte>.Shared.Return(buffer,clearArray:true);buffer=next;
    }
    public void Dispose()
    {
        if(buffer.Length==0)return;
        ArrayPool<byte>.Shared.Return(buffer,clearArray:true);buffer=[];digest.Dispose();
    }
}
