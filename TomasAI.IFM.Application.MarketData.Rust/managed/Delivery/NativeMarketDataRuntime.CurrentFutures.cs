using System.Buffers;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

[MessagePackObject(AllowPrivate = true)]
internal sealed record NativeCurrentFuturesResult(
    [property: Key(0)] FuturesContractV3ReadModel[]? Contracts,
    [property: Key(1)] string? Failure,
    [property: Key(2)] string Identity);

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Copies already-selected native contract DTOs. Failure codes are
    /// mapped to existing exception types; C# performs no eligibility/currency policy.</summary>
    internal unsafe FuturesContractV3ReadModel[] SelectCurrentFutures(ulong token, string symbol, DateOnly date, int count, string? fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var call = library.CatalogCurrent ?? throw new NotSupportedException("Native current futures selection is unavailable.");
        var input = JsonSerializer.SerializeToUtf8Bytes(new { symbol, date = date.Year * 10000 + date.Month * 100 + date.Day, count, fallback });
        if (input.Length > 4096) throw new ArgumentOutOfRangeException(nameof(symbol));
        const int maximum = 2 * 1024 * 1024;
        var buffer = ArrayPool<byte>.Shared.Rent(maximum);
        try
        {
            lock (gate)
            {
                EnsureLiveAccess(); uint length;
                fixed (byte* request = input) fixed (byte* output = buffer)
                    NativeMarketDataLibrary.Check(call(handle.Identity, token, request, (uint)input.Length, output, maximum, out length), "select native current futures");
                if (length > maximum) throw new InvalidDataException("Native current futures exceed their query bound.");
                var result = MessagePackSerializer.Deserialize<NativeCurrentFuturesResult>(buffer.AsMemory(0, (int)length),
                    MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
                return result.Failure switch
                {
                    null => result.Contracts ?? throw new InvalidDataException("Missing native current futures."),
                    "NotFound" => throw new OnTheRunFuturesContractNotFoundException(result.Identity, date),
                    "MissingCurrency" => throw new MarketDataContractMappingException(result.Identity, "both the contract currency and settlement currency are missing"),
                    "InvalidMaturity" => throw new DatabentoFeedException(DatabentoFeedStatus.AbiMismatch, "A native contract detail marked an invalid maturity date as present."),
                    _ => throw new InvalidDataException("Unknown native current futures failure.")
                };
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }
}
