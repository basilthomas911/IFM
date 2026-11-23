using System.Buffers;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

internal sealed class SelectionFingerprintBuffer : IBufferWriter<byte>, IDisposable
{
    private byte[] _bytes = ArrayPool<byte>.Shared.Rent(16384);
    private int _written;

    public ReadOnlySpan<byte> Written => _bytes.AsSpan(0, _written);

    public void Advance(int count)
    {
        if (count < 0 || count > _bytes.Length - _written)
            throw new ArgumentOutOfRangeException(nameof(count));
        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));
        var required = checked(_written + Math.Max(1, sizeHint));
        if (required > _bytes.Length)
        {
            var next = ArrayPool<byte>.Shared.Rent(Math.Max(required, checked(_bytes.Length * 2)));
            Written.CopyTo(next);
            ReturnBuffer();
            _bytes = next;
        }
        return _bytes.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

    public void Dispose()
    {
        if (_bytes.Length == 0) return;
        ReturnBuffer();
        _bytes = [];
        _written = 0;
    }

    private void ReturnBuffer()
    {
        _bytes.AsSpan(0, _written).Clear();
        ArrayPool<byte>.Shared.Return(_bytes);
    }
}
