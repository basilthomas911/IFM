using System.Buffers;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Interop;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>Complete provider HTTP warning with correlation metadata. Credentials
/// have already been redacted by the native transport.</summary>
public sealed record HistoricalWarning(string Endpoint, ushort Status, string RequestId, string Text);

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Consumes at most sixteen pending HTTP warnings. Signal bit four
    /// in WaitForLiveChangesAsync announces these records independently of price
    /// queues. Drain until empty after a wake; no timer polling is required.</summary>
    public unsafe HistoricalWarning[] DrainHistoricalWarnings()
    {
        lock (gate)
        {
            EnsureLiveAccess();
            var buffer = ArrayPool<byte>.Shared.Rent(65_536);
            try
            {
                var result = new List<HistoricalWarning>();
                for (var index = 0; index < 16; index++)
                {
                    uint length;
                    fixed (byte* output = buffer)
                        NativeMarketDataLibrary.Check(library.HistoryWarning(handle.Identity, output, 65_536, out length), "drain historical warning");
                    if (length == 0) break;
                    if (length > 65_536) throw new InvalidDataException("Native HTTP warning exceeded its buffer.");
                    using var json = JsonDocument.Parse(buffer.AsMemory(0, checked((int)length)));
                    var value = json.RootElement;
                    result.Add(new(value.GetProperty("endpoint").GetString()!, value.GetProperty("status").GetUInt16(),
                        value.GetProperty("request_id").GetString()!, value.GetProperty("text").GetString()!));
                }
                return result.ToArray();
            }
            finally { ArrayPool<byte>.Shared.Return(buffer, true); }
        }
    }
}
