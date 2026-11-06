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
    const string AlreadySeen = "OPTION_EVIDENCE_ALREADY_SEEN";
    /// <summary>Validates the exact existing evidence/idempotency contract and
    /// acknowledges only the completed durable write. The token belongs to this
    /// external consumer, never to a native resource or dataset control gate.</summary>
    public async ValueTask<NativeEvidenceReceipt> WriteAsync(NativeEvidenceWrite request, CancellationToken stoppingToken)
    {
        var evidence = Validate(request, stoppingToken);
        var accepted = await writer.WriteFirstAsync(evidence, stoppingToken).ConfigureAwait(false);
        return Receipt(request, accepted);
    }

    /// <summary>Persists a bounded group and keeps each validation or write outcome correlated to its own receipt.</summary>
    public async ValueTask<(NativeEvidenceReceipt Receipt, Exception? Error)[]> WriteBatchAsync(
        IReadOnlyList<NativeEvidenceWrite> requests, CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count is < 1 or > IOptionTradeEvidenceWriter.MaximumFirstWriteBatch)
            throw new ArgumentOutOfRangeException(nameof(requests));
        if (requests.Count == 1)
        {
            try { return [(await WriteAsync(requests[0], stoppingToken).ConfigureAwait(false), null)]; }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested) { return [(Failed(requests[0]), error)]; }
        }
        var results = new (NativeEvidenceReceipt Receipt, Exception? Error)[requests.Count];
        var valid = new List<(int Index, OptionTradeEvidence Evidence)>(requests.Count);
        for (var index = 0; index < requests.Count; index++)
        {
            try { valid.Add((index, Validate(requests[index], stoppingToken))); }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                results[index] = (Failed(requests[index]), error);
            }
        }

        if (valid.Count == 0) return results;
        try
        {
            var accepted = await writer.WriteFirstBatchAsync(valid.Select(static item => item.Evidence).ToArray(), stoppingToken)
                .ConfigureAwait(false);
            if (accepted.Length != valid.Count)
                throw new InvalidOperationException("Durable option evidence results do not match the batch.");
            for (var index = 0; index < valid.Count; index++)
            {
                var requestIndex = valid[index].Index;
                results[requestIndex] = (Receipt(requests[requestIndex], accepted[index]), null);
            }
        }
        catch (Exception error) when (!stoppingToken.IsCancellationRequested)
        {
            foreach (var item in valid)
                results[item.Index] = (Failed(requests[item.Index]), error);
        }
        return results;
    }

    static OptionTradeEvidence Validate(NativeEvidenceWrite request, CancellationToken stoppingToken)
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
        return evidence;
    }

    static NativeEvidenceReceipt Receipt(NativeEvidenceWrite request, bool accepted)
        => new(request.Receipt, request.Identity, request.Digest, accepted, accepted ? null : AlreadySeen);

    static NativeEvidenceReceipt Failed(NativeEvidenceWrite request)
        => new(request.Receipt, request.Identity, request.Digest, false, "Durable option evidence persistence failed.");

    static bool Hash(string value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
