using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Completion acknowledges durable source plus the first immutable pricing attempt, not a transient enqueue.</summary>
public interface IOptionTradeEvidenceWriter
{
    const int MaximumFirstWriteBatch = 4;

    /// <summary>Persists source evidence and its first immutable pricing attempt.</summary>
    /// <param name="evidence">The qualified evidence and pricing attempt.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>Completion after the attempt is durable.</returns>
    ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken);

    /// <summary>Accepts the first attempt or its exact retry. Returns false when a different attempt already owns the source identity.</summary>
    /// <param name="evidence">The qualified evidence and pricing attempt.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>True for the accepted attempt or its exact retry.</returns>
    async ValueTask<bool> WriteFirstAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken)
    {
        await WriteAsync(evidence, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Confirms each durable first attempt or exact retry in request order.</summary>
    /// <param name="evidence">The bounded batch of qualified attempts.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The acceptance flag for each input in order.</returns>
    async ValueTask<bool[]> WriteFirstBatchAsync(IReadOnlyList<OptionTradeEvidence> evidence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Count is < 1 or > MaximumFirstWriteBatch)
            throw new ArgumentOutOfRangeException(nameof(evidence));
        var accepted = new bool[evidence.Count];
        for (var index = 0; index < evidence.Count; index++)
            accepted[index] = await WriteFirstAsync(evidence[index], cancellationToken).ConfigureAwait(false);
        return accepted;
    }
}
