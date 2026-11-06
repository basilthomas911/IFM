using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Completion acknowledges durable source plus the first immutable pricing attempt, not a transient enqueue.</summary>
public interface IOptionTradeEvidenceWriter
{
    const int MaximumFirstWriteBatch = 4;

    ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken);

    /// <summary>Accepts the first attempt or its exact retry. Returns false when a different attempt already owns the source identity.</summary>
    async ValueTask<bool> WriteFirstAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken)
    {
        await WriteAsync(evidence, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Confirms each durable first attempt or exact retry in request order.</summary>
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
