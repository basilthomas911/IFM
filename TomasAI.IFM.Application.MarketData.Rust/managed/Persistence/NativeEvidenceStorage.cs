using MessagePack;
using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Rust.Persistence;

/// <summary>Copied native handoff. This record contains no native handle, lease,
/// callback or cancellation authority over a dataset.</summary>
[MessagePackObject]
public sealed record NativeEvidenceWrite(
    [property: Key(0)] ulong Receipt,
    [property: Key(1)] string Identity,
    [property: Key(2)] string Digest,
    [property: Key(3)] byte[] Content);

/// <summary>A positive result means the existing durable evidence writer completed,
/// not that a broker accepted a packet. Receipt and both hashes fence late replies.</summary>
[MessagePackObject]
public sealed record NativeEvidenceReceipt(
    [property: Key(0)] ulong Receipt,
    [property: Key(1)] string Identity,
    [property: Key(2)] string Digest,
    [property: Key(3)] bool Persisted,
    [property: Key(4)] string? Error);

/// <summary>External IO consumer of immutable evidence. Rust publishes the copied
/// request over NATS. No streaming payload crosses the FFI and a stalled writer
/// cannot prevent Rust cancelling and joining its independent dataset resources.</summary>
public sealed class NativeEvidenceStorage(IOptionTradeEvidenceWriter writer)
{
    /// <summary>Validates the exact existing evidence/idempotency contract and
    /// acknowledges only the completed durable write. The token belongs to this
    /// external consumer, never to a native resource or dataset control gate.</summary>
    public async ValueTask<NativeEvidenceReceipt> WriteAsync(NativeEvidenceWrite request, CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Receipt == 0 || !Hash(request.Identity) || !Hash(request.Digest)
            || request.Content is not { Length: > 0 and <= 131072 })
            throw new InvalidDataException("Invalid native evidence handoff identity or capacity.");
        var evidence = MessagePackSerializer.Deserialize<OptionTradeEvidence>(request.Content,
            MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData), stoppingToken);
        evidence.Validate();
        if (!string.Equals(evidence.Source.Identity, request.Identity, StringComparison.Ordinal)
            || !string.Equals(evidence.Source.SourceDigest, request.Digest, StringComparison.Ordinal))
            throw new InvalidDataException("Native evidence receipt differs from its immutable source.");
        await writer.WriteAsync(evidence, stoppingToken).ConfigureAwait(false);
        return new(request.Receipt, request.Identity, request.Digest, true, null);
    }

    static bool Hash(string value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
